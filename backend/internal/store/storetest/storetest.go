// Package storetest 是 store.Store 的一致性测试套件。
//
// 套件只通过接口断言行为，不依赖具体实现（JSON 文件、SQLite 或内存实现都应逐条通过），
// 因此它同时承担两个职责：固定当前对外可见的行为，以及作为更换存储实现时的护栏。
package storetest

import (
	"errors"
	"fmt"
	"strings"
	"sync"
	"testing"
	"time"

	"unity-3d-inspection/backend/internal/model"
	"unity-3d-inspection/backend/internal/store"
)

// Factory 描述如何获得被测的存储实例。
type Factory struct {
	// Open 返回一个空存储，以及一个「在同一持久化位置上重新打开」的函数。
	// 重新打开用于验证写入是否真正落盘，而不是只留在进程内存里。
	Open func(t *testing.T) (st store.Store, reopen func() store.Store)
}

// Run 依次跑完全部一致性用例。
func Run(t *testing.T, f Factory) {
	t.Helper()

	t.Run("空库返回非 nil 空切片", func(t *testing.T) {
		st := open(t, f)

		list, err := st.List()
		if err != nil {
			t.Fatalf("查询失败：%v", err)
		}
		if list == nil {
			t.Fatal("空库应返回非 nil 的空切片，否则 HTTP 层会序列化成 null")
		}
		if len(list) != 0 {
			t.Fatalf("空库应返回 0 条，实际 %d", len(list))
		}
	})

	t.Run("Create 补全服务端时间且两字段相同", func(t *testing.T) {
		st := open(t, f)

		created := mustCreate(t, st, fixture("a1"))

		if created.CreatedAt == "" || created.UpdatedAt == "" {
			t.Fatalf("时间字段为空：%+v", created)
		}
		if created.CreatedAt != created.UpdatedAt {
			t.Fatalf("新建问题的两个时间字段应相同：createdAt=%s updatedAt=%s", created.CreatedAt, created.UpdatedAt)
		}
		assertStamp(t, created.CreatedAt)
	})

	t.Run("时间字段定宽且按字符串即可排序", func(t *testing.T) {
		st := open(t, f)

		earlier := mustCreate(t, st, fixture("a1"))
		time.Sleep(5 * time.Millisecond)
		later := mustCreate(t, st, fixture("a2"))

		for _, stamp := range []string{earlier.CreatedAt, later.CreatedAt} {
			if len(stamp) != 24 {
				t.Fatalf("时间格式应定宽 24 字符（毫秒精度 UTC），实际 %q 长度 %d", stamp, len(stamp))
			}
			if !strings.HasSuffix(stamp, "Z") {
				t.Fatalf("时间必须以 Z 结尾表示 UTC，实际 %q", stamp)
			}
		}
		if !(earlier.CreatedAt < later.CreatedAt) {
			t.Fatalf("后写入的时间应更大：%s vs %s", earlier.CreatedAt, later.CreatedAt)
		}
	})

	t.Run("Create 保留调用方给定的状态", func(t *testing.T) {
		st := open(t, f)

		input := fixture("a1")
		input.Status = model.StatusResolved

		created := mustCreate(t, st, input)
		if created.Status != model.StatusResolved {
			t.Fatalf("存储层不应改写状态，实际 %s", created.Status)
		}

		list := mustList(t, st)
		if list[0].Status != model.StatusResolved {
			t.Fatalf("落盘后状态被改写：%s", list[0].Status)
		}
	})

	t.Run("Create 原样保留业务字段", func(t *testing.T) {
		st := open(t, f)

		input := fixture("a1")
		input.Title = "  标题两侧空格应原样保留  "
		input.Description = "第一行\n第二行"
		input.Priority = model.PriorityLow
		input.DeviceID = "CB-101"
		input.Area = "A区 1F"
		input.Location = model.Location{X: 3.003469944000244, Y: 1.206104040145874, Z: -3.0999999046325684}

		created := mustCreate(t, st, input)
		assertSameIssue(t, input, created)

		list := mustList(t, st)
		assertSameIssue(t, input, list[0])
	})

	t.Run("Create 的返回值与存储内部状态互相独立", func(t *testing.T) {
		st := open(t, f)

		created := mustCreate(t, st, fixture("a1"))
		created.Title = "被外部改动"
		created.Location.X = 999

		list := mustList(t, st)
		if list[0].Title != "机柜A顶部渗水" {
			t.Fatalf("返回值被修改后污染了存储：%s", list[0].Title)
		}
		if list[0].Location.X != 1.5 {
			t.Fatalf("返回值被修改后污染了坐标：%v", list[0].Location)
		}
	})

	t.Run("List 按创建时间倒序、同一时刻按标识倒序", func(t *testing.T) {
		st := open(t, f)

		for i := 0; i < 6; i++ {
			mustCreate(t, st, fixture(fmt.Sprintf("id-%02d", i)))
		}

		list := mustList(t, st)
		if len(list) != 6 {
			t.Fatalf("期望 6 条，实际 %d", len(list))
		}
		for i := 1; i < len(list); i++ {
			prev, cur := list[i-1], list[i]
			if prev.CreatedAt < cur.CreatedAt {
				t.Fatalf("第 %d 条创建时间比前一条新：%s vs %s", i, cur.CreatedAt, prev.CreatedAt)
			}
			if prev.CreatedAt == cur.CreatedAt && prev.ID < cur.ID {
				t.Fatalf("同一时刻应按标识倒序，第 %d 条为 %s，前一条为 %s", i, cur.ID, prev.ID)
			}
		}
	})

	t.Run("List 返回的是副本", func(t *testing.T) {
		st := open(t, f)
		mustCreate(t, st, fixture("a1"))

		first := mustList(t, st)
		first[0].Title = "被外部改动"
		first[0].Location.Z = 42

		second := mustList(t, st)
		if second[0].Title != "机柜A顶部渗水" || second[0].Location.Z != -3.25 {
			t.Fatalf("List 返回的切片与内部状态共享了内存：%+v", second[0])
		}
	})

	t.Run("UpdateStatus 只刷新 updatedAt", func(t *testing.T) {
		st := open(t, f)
		created := mustCreate(t, st, fixture("a1"))

		time.Sleep(5 * time.Millisecond)
		updated, err := st.UpdateStatus("a1", model.StatusInProgress)
		if err != nil {
			t.Fatalf("更新状态失败：%v", err)
		}
		if updated.Status != model.StatusInProgress {
			t.Fatalf("状态未更新：%s", updated.Status)
		}
		if updated.CreatedAt != created.CreatedAt {
			t.Fatalf("createdAt 被改动：%s -> %s", created.CreatedAt, updated.CreatedAt)
		}
		if !(updated.UpdatedAt > created.UpdatedAt) {
			t.Fatalf("updatedAt 未刷新：%s -> %s", created.UpdatedAt, updated.UpdatedAt)
		}
		assertStamp(t, updated.UpdatedAt)
	})

	t.Run("UpdateStatus 未知标识返回 ErrNotFound", func(t *testing.T) {
		st := open(t, f)

		if _, err := st.UpdateStatus("missing", model.StatusOpen); !errors.Is(err, store.ErrNotFound) {
			t.Fatalf("期望 ErrNotFound，实际 %v", err)
		}
	})

	t.Run("UpdateStatus 不校验状态取值", func(t *testing.T) {
		st := open(t, f)
		mustCreate(t, st, fixture("a1"))

		// 状态机属于 model 层的 ParseStatus，存储层只负责落盘，这里固定该分工。
		if _, err := st.UpdateStatus("a1", model.Status("weird")); err != nil {
			t.Fatalf("存储层不应拒绝任意状态字符串：%v", err)
		}
		if list := mustList(t, st); list[0].Status != model.Status("weird") {
			t.Fatalf("状态未按原样保存：%q", list[0].Status)
		}
	})

	t.Run("Delete 未知标识返回 ErrNotFound", func(t *testing.T) {
		st := open(t, f)

		if err := st.Delete("missing"); !errors.Is(err, store.ErrNotFound) {
			t.Fatalf("期望 ErrNotFound，实际 %v", err)
		}
	})

	t.Run("Delete 只删除目标条目", func(t *testing.T) {
		st := open(t, f)
		mustCreate(t, st, fixture("keep"))
		mustCreate(t, st, fixture("drop"))

		if err := st.Delete("drop"); err != nil {
			t.Fatalf("删除失败：%v", err)
		}

		list := mustList(t, st)
		if len(list) != 1 || list[0].ID != "keep" {
			t.Fatalf("删除结果不符：%+v", list)
		}
	})

	t.Run("重复标识追加而不覆盖且只作用于最早一条", func(t *testing.T) {
		st := open(t, f)

		first := fixture("dup")
		first.Title = "第一条"
		second := fixture("dup")
		second.Title = "第二条"
		mustCreate(t, st, first)
		mustCreate(t, st, second)

		list := mustList(t, st)
		if len(list) != 2 {
			t.Fatalf("重复标识应追加为两条，实际 %d 条", len(list))
		}

		updated, err := st.UpdateStatus("dup", model.StatusResolved)
		if err != nil {
			t.Fatalf("更新失败：%v", err)
		}
		if updated.Title != "第一条" {
			t.Fatalf("更新应命中最早写入的一条，实际命中 %q", updated.Title)
		}

		if err := st.Delete("dup"); err != nil {
			t.Fatalf("删除失败：%v", err)
		}
		remaining := mustList(t, st)
		if len(remaining) != 1 || remaining[0].Title != "第二条" || remaining[0].Status != model.StatusOpen {
			t.Fatalf("删除应移除最早写入的一条：%+v", remaining)
		}

		if err := st.Delete("dup"); err != nil {
			t.Fatalf("删除最后一条失败：%v", err)
		}
		if list := mustList(t, st); len(list) != 0 {
			t.Fatalf("清理后应为空：%+v", list)
		}
	})

	t.Run("重新打开后数据仍在", func(t *testing.T) {
		st, reopen := f.Open(t)

		mustCreate(t, st, fixture("keep"))
		mustCreate(t, st, fixture("drop"))
		if _, err := st.UpdateStatus("keep", model.StatusResolved); err != nil {
			t.Fatalf("更新失败：%v", err)
		}
		if err := st.Delete("drop"); err != nil {
			t.Fatalf("删除失败：%v", err)
		}

		reloaded := reopen()
		list := mustList(t, reloaded)
		if len(list) != 1 {
			t.Fatalf("重开后应有 1 条，实际 %d", len(list))
		}
		if list[0].ID != "keep" || list[0].Status != model.StatusResolved {
			t.Fatalf("重开后字段不符：%+v", list[0])
		}
		if list[0].Title != "机柜A顶部渗水" || list[0].Location.Z != -3.25 {
			t.Fatalf("重开后内容丢失：%+v", list[0])
		}
	})

	t.Run("重新打开空库仍是空切片", func(t *testing.T) {
		_, reopen := f.Open(t)

		list, err := reopen().List()
		if err != nil {
			t.Fatalf("查询失败：%v", err)
		}
		if list == nil || len(list) != 0 {
			t.Fatalf("重开空库应返回空切片：%#v", list)
		}
	})

	t.Run("并发创建全部落盘", func(t *testing.T) {
		st := open(t, f)

		const total = 40
		var wg sync.WaitGroup
		errs := make(chan error, total)
		for i := 0; i < total; i++ {
			wg.Add(1)
			go func(i int) {
				defer wg.Done()
				if _, err := st.Create(fixture(fmt.Sprintf("id-%02d", i))); err != nil {
					errs <- err
				}
			}(i)
		}
		wg.Wait()
		close(errs)
		for err := range errs {
			t.Fatalf("并发创建失败：%v", err)
		}

		if list := mustList(t, st); len(list) != total {
			t.Fatalf("期望 %d 条，实际 %d", total, len(list))
		}
	})

	t.Run("浮点坐标精确往返", func(t *testing.T) {
		st := open(t, f)

		input := fixture("a1")
		input.Location = model.Location{X: 3.003469944000244, Y: -1.5840773582458496, Z: 10.439970016479492}

		mustCreate(t, st, input)
		got := mustList(t, st)[0].Location
		if got != input.Location {
			t.Fatalf("坐标未精确往返：期望 %+v，实际 %+v", input.Location, got)
		}
	})

	t.Run("大文本与多字节字符往返", func(t *testing.T) {
		st := open(t, f)

		input := fixture("a1")
		input.Title = "柜顶渗水 🔧 复刻联调"
		input.Description = strings.Repeat("渗水点已封堵，待复查。\n", 200)

		mustCreate(t, st, input)
		got := mustList(t, st)[0]
		if got.Title != input.Title {
			t.Fatalf("多字节标题未往返：%q", got.Title)
		}
		if got.Description != input.Description {
			t.Fatalf("长描述未往返：长度 %d vs %d", len(got.Description), len(input.Description))
		}
	})
}

func open(t *testing.T, f Factory) store.Store {
	t.Helper()
	st, _ := f.Open(t)
	return st
}

func fixture(id string) model.Issue {
	return model.Issue{
		ID:          id,
		Title:       "机柜A顶部渗水",
		Description: "柜顶有水渍",
		Priority:    model.PriorityHigh,
		Status:      model.StatusOpen,
		DeviceID:    "CB-101",
		Area:        "A区 1F",
		Location:    model.Location{X: 1.5, Y: 2, Z: -3.25},
	}
}

func mustCreate(t *testing.T, st store.Store, issue model.Issue) model.Issue {
	t.Helper()
	created, err := st.Create(issue)
	if err != nil {
		t.Fatalf("创建失败：%v", err)
	}
	return created
}

func mustList(t *testing.T, st store.Store) []model.Issue {
	t.Helper()
	list, err := st.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	return list
}

func assertStamp(t *testing.T, raw string) {
	t.Helper()
	if _, err := time.Parse(model.TimeLayout, raw); err != nil {
		t.Fatalf("时间字段不可按 model.TimeLayout 解析：%q（%v）", raw, err)
	}
}

func assertSameIssue(t *testing.T, want, got model.Issue) {
	t.Helper()
	if got.ID != want.ID {
		t.Fatalf("标识不符：%q vs %q", got.ID, want.ID)
	}
	if got.Title != want.Title {
		t.Fatalf("标题不符：%q vs %q", got.Title, want.Title)
	}
	if got.Description != want.Description {
		t.Fatalf("描述不符：%q vs %q", got.Description, want.Description)
	}
	if got.Priority != want.Priority {
		t.Fatalf("优先级不符：%q vs %q", got.Priority, want.Priority)
	}
	if got.DeviceID != want.DeviceID || got.Area != want.Area {
		t.Fatalf("设备或区域不符：%q/%q vs %q/%q", got.DeviceID, got.Area, want.DeviceID, want.Area)
	}
	if got.Location != want.Location {
		t.Fatalf("坐标不符：%+v vs %+v", got.Location, want.Location)
	}
}
