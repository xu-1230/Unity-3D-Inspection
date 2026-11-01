package migrate

import (
	"encoding/json"
	"os"
	"path/filepath"
	"testing"

	"unity-3d-inspection/backend/internal/model"
	"unity-3d-inspection/backend/internal/store"
)

func writeJSON(t *testing.T, issues []model.Issue) string {
	t.Helper()
	path := filepath.Join(t.TempDir(), "issues.json")
	data, err := json.MarshalIndent(issues, "", "  ")
	if err != nil {
		t.Fatalf("序列化失败：%v", err)
	}
	if err := os.WriteFile(path, data, 0o644); err != nil {
		t.Fatalf("写入迁移源失败：%v", err)
	}
	return path
}

func sampleIssues() []model.Issue {
	return []model.Issue{
		{
			ID:          "9f9b64fee2dd99758226540233fcb3d4",
			Title:       "机柜A顶部渗水",
			Description: "柜顶有水渍",
			Priority:    model.PriorityHigh,
			Status:      model.StatusInProgress,
			DeviceID:    "CB-101",
			Area:        "A区 1F",
			Location:    model.Location{X: 1.5, Y: 2, Z: -3.25},
			CreatedAt:   "2026-10-30T11:37:37.000Z",
			UpdatedAt:   "2026-10-31T15:13:12.072Z",
		},
		{
			ID:          "3e0cdc910d3bb5d243ba19b67078f9db",
			Title:       "柜B漏水",
			Description: "",
			Priority:    model.PriorityLow,
			Status:      model.StatusResolved,
			Location:    model.Location{X: -1.5, Y: 0, Z: 3},
			CreatedAt:   "2026-10-30T11:38:18.514Z",
			UpdatedAt:   "2026-10-30T11:38:18.514Z",
		},
	}
}

func TestFromJSONPreservesEverything(t *testing.T) {
	source := writeJSON(t, sampleIssues())
	dbPath := filepath.Join(t.TempDir(), "data", "issues.db")

	report, err := FromJSON(source, dbPath)
	if err != nil {
		t.Fatalf("迁移失败：%v", err)
	}
	if report.Read != 2 || report.Inserted != 2 || report.Skipped != 0 {
		t.Fatalf("迁移计数不符：%+v", report)
	}

	st, err := store.NewSQLiteStore(dbPath)
	if err != nil {
		t.Fatalf("打开目标库失败：%v", err)
	}
	defer st.Close()

	list, err := st.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	if len(list) != 2 {
		t.Fatalf("期望 2 条，实际 %d", len(list))
	}
	// 列表按创建时间倒序，第二条（较新）在前。
	if list[0].ID != "3e0cdc910d3bb5d243ba19b67078f9db" || list[1].Title != "机柜A顶部渗水" {
		t.Fatalf("顺序不符：%+v", list)
	}
	if list[1].CreatedAt != "2026-10-30T11:37:37.000Z" || list[1].UpdatedAt != "2026-10-31T15:13:12.072Z" {
		t.Fatalf("时间字段未原样保留：%+v", list[1])
	}
	if list[1].Location != (model.Location{X: 1.5, Y: 2, Z: -3.25}) {
		t.Fatalf("坐标未原样保留：%+v", list[1].Location)
	}
	if list[1].Status != model.StatusInProgress || list[1].Priority != model.PriorityHigh {
		t.Fatalf("枚举字段未原样保留：%+v", list[1])
	}
}

func TestFromJSONIsIdempotent(t *testing.T) {
	source := writeJSON(t, sampleIssues())
	dbPath := filepath.Join(t.TempDir(), "data", "issues.db")

	if _, err := FromJSON(source, dbPath); err != nil {
		t.Fatalf("首次迁移失败：%v", err)
	}

	report, err := FromJSON(source, dbPath)
	if err != nil {
		t.Fatalf("二次迁移失败：%v", err)
	}
	if report.Read != 2 || report.Inserted != 0 || report.Skipped != 2 {
		t.Fatalf("重复迁移应全部跳过：%+v", report)
	}

	st, err := store.NewSQLiteStore(dbPath)
	if err != nil {
		t.Fatalf("打开目标库失败：%v", err)
	}
	defer st.Close()

	list, err := st.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	if len(list) != 2 {
		t.Fatalf("重复迁移不应产生副本，实际 %d 条", len(list))
	}
}

func TestFromJSONRejectsBrokenSource(t *testing.T) {
	tests := []struct {
		name  string
		write func(t *testing.T) string
	}{
		{
			name: "文件不存在",
			write: func(t *testing.T) string {
				return filepath.Join(t.TempDir(), "missing.json")
			},
		},
		{
			name: "非法 JSON",
			write: func(t *testing.T) string {
				path := filepath.Join(t.TempDir(), "bad.json")
				if err := os.WriteFile(path, []byte(`[{"id":`), 0o644); err != nil {
					t.Fatalf("写入失败：%v", err)
				}
				return path
			},
		},
		{
			name: "空文件",
			write: func(t *testing.T) string {
				path := filepath.Join(t.TempDir(), "empty.json")
				if err := os.WriteFile(path, nil, 0o644); err != nil {
					t.Fatalf("写入失败：%v", err)
				}
				return path
			},
		},
		{
			name: "记录缺少标识",
			write: func(t *testing.T) string {
				return writeJSON(t, []model.Issue{{Title: "x", CreatedAt: "2026-01-01T00:00:00.000Z", UpdatedAt: "2026-01-01T00:00:00.000Z"}})
			},
		},
		{
			name: "记录缺少时间",
			write: func(t *testing.T) string {
				return writeJSON(t, []model.Issue{{ID: "a1"}})
			},
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			dbPath := filepath.Join(t.TempDir(), "data", "issues.db")

			if _, err := FromJSON(tc.write(t), dbPath); err == nil {
				t.Fatal("期望返回错误，实际成功")
			}
		})
	}
}
