// Package migrate 负责把旧版本遗留的 JSON 数据文件一次性导入 SQLite。
//
// 迁移是一种显式动作：既不参与服务启动，也不依赖宿主机上某个文件是否存在，
// 重复执行时按「标识 + 创建时间」判定已导入并跳过。
package migrate

import (
	"encoding/json"
	"fmt"
	"os"

	"unity-3d-inspection/backend/internal/model"
	"unity-3d-inspection/backend/internal/store"
)

// Report 是一次迁移的结果。
type Report struct {
	// Read 是从 JSON 文件读到的记录数。
	Read int
	// Inserted 是本次新写入数据库的记录数。
	Inserted int
	// Skipped 是数据库中已存在而跳过的记录数。
	Skipped int
}

// FromJSON 读取 JSON 文件并写入 SQLite，目标库不存在时按同一套建表语句创建。
func FromJSON(jsonPath, dbPath string) (Report, error) {
	var report Report

	data, err := os.ReadFile(jsonPath)
	if err != nil {
		return report, fmt.Errorf("读取迁移源失败：%w", err)
	}
	if len(data) == 0 {
		return report, fmt.Errorf("迁移源为空：%s", jsonPath)
	}

	var issues []model.Issue
	if err := json.Unmarshal(data, &issues); err != nil {
		return report, fmt.Errorf("解析迁移源失败：%w", err)
	}
	report.Read = len(issues)

	for i, issue := range issues {
		if issue.ID == "" {
			return report, fmt.Errorf("第 %d 条记录缺少 id，无法迁移", i+1)
		}
		if issue.CreatedAt == "" || issue.UpdatedAt == "" {
			return report, fmt.Errorf("第 %d 条记录（%s）缺少时间字段，无法迁移", i+1, issue.ID)
		}
	}

	st, err := store.NewSQLiteStore(dbPath)
	if err != nil {
		return report, fmt.Errorf("打开目标库失败：%w", err)
	}
	defer st.Close()

	inserted, skipped, err := st.Import(issues)
	if err != nil {
		return report, fmt.Errorf("写入目标库失败：%w", err)
	}
	report.Inserted = inserted
	report.Skipped = skipped
	return report, nil
}
