package store

import (
	"errors"
	"fmt"
	"path/filepath"
	"sync"
	"testing"
	"time"

	"unity-3d-inspection/backend/internal/model"
)

func newStore(t *testing.T) *FileStore {
	t.Helper()
	path := filepath.Join(t.TempDir(), "data", "issues.json")
	s, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("创建存储失败：%v", err)
	}
	return s
}

func issueFixture(id string) model.Issue {
	return model.Issue{
		ID:          id,
		Title:       "机柜A顶部渗水",
		Description: "柜顶有水渍",
		Priority:    model.PriorityHigh,
		Status:      model.StatusOpen,
		Location:    model.Location{X: 1, Y: 2, Z: 3},
	}
}

func TestFileStoreCreateAssignsServerTimestamps(t *testing.T) {
	s := newStore(t)
	fixed := time.Date(2026, 10, 30, 8, 12, 33, 0, time.UTC)
	s.now = func() time.Time { return fixed }

	created, err := s.Create(issueFixture("a1"))
	if err != nil {
		t.Fatalf("创建失败：%v", err)
	}

	want := "2026-10-30T08:12:33.000Z"
	if created.CreatedAt != want || created.UpdatedAt != want {
		t.Fatalf("时间字段不符：createdAt=%s updatedAt=%s", created.CreatedAt, created.UpdatedAt)
	}
	if created.Status != model.StatusOpen {
		t.Fatalf("状态应为 open，实际 %s", created.Status)
	}
}

func TestFileStoreListReturnsNewestFirst(t *testing.T) {
	s := newStore(t)
	base := time.Date(2026, 10, 30, 8, 0, 0, 0, time.UTC)
	step := 0
	s.now = func() time.Time {
		step++
		return base.Add(time.Duration(step) * time.Minute)
	}

	if _, err := s.Create(issueFixture("older")); err != nil {
		t.Fatalf("创建失败：%v", err)
	}
	if _, err := s.Create(issueFixture("newer")); err != nil {
		t.Fatalf("创建失败：%v", err)
	}

	list, err := s.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	if len(list) != 2 {
		t.Fatalf("期望 2 条，实际 %d", len(list))
	}
	if list[0].ID != "newer" {
		t.Fatalf("最新问题应排在最前，实际首位 %s", list[0].ID)
	}
}

func TestFileStorePersistsAcrossReload(t *testing.T) {
	path := filepath.Join(t.TempDir(), "data", "issues.json")
	first, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("创建存储失败：%v", err)
	}
	if _, err := first.Create(issueFixture("a1")); err != nil {
		t.Fatalf("创建失败：%v", err)
	}
	if _, err := first.Create(issueFixture("a2")); err != nil {
		t.Fatalf("创建失败：%v", err)
	}

	reloaded, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("重新加载失败：%v", err)
	}
	list, err := reloaded.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	if len(list) != 2 {
		t.Fatalf("重启后应有 2 条，实际 %d", len(list))
	}
	if list[0].Title != "机柜A顶部渗水" || list[0].Location.Z != 3 {
		t.Fatalf("重启后字段丢失：%+v", list[0])
	}
}

func TestFileStoreUpdateStatusRefreshesOnlyUpdatedAt(t *testing.T) {
	path := filepath.Join(t.TempDir(), "data", "issues.json")
	s, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("创建存储失败：%v", err)
	}
	created, err := s.Create(issueFixture("a1"))
	if err != nil {
		t.Fatalf("创建失败：%v", err)
	}

	later := time.Date(2026, 10, 30, 9, 40, 7, 0, time.UTC)
	s.now = func() time.Time { return later }

	updated, err := s.UpdateStatus("a1", model.StatusResolved)
	if err != nil {
		t.Fatalf("改状态失败：%v", err)
	}
	if updated.Status != model.StatusResolved {
		t.Fatalf("状态未更新：%s", updated.Status)
	}
	if updated.CreatedAt != created.CreatedAt {
		t.Fatalf("createdAt 被改动：%s -> %s", created.CreatedAt, updated.CreatedAt)
	}
	if updated.UpdatedAt != "2026-10-30T09:40:07.000Z" {
		t.Fatalf("updatedAt 未刷新：%s", updated.UpdatedAt)
	}

	reloaded, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("重新加载失败：%v", err)
	}
	list, err := reloaded.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	if list[0].Status != model.StatusResolved {
		t.Fatalf("重启后状态回退：%s", list[0].Status)
	}
}

func TestFileStoreUpdateStatusUnknownID(t *testing.T) {
	s := newStore(t)

	_, err := s.UpdateStatus("missing", model.StatusOpen)

	if !errors.Is(err, ErrNotFound) {
		t.Fatalf("期望 ErrNotFound，实际 %v", err)
	}
}

func TestFileStoreDeleteRemovesAndPersists(t *testing.T) {
	path := filepath.Join(t.TempDir(), "data", "issues.json")
	s, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("创建存储失败：%v", err)
	}
	if _, err := s.Create(issueFixture("keep")); err != nil {
		t.Fatalf("创建失败：%v", err)
	}
	if _, err := s.Create(issueFixture("drop")); err != nil {
		t.Fatalf("创建失败：%v", err)
	}

	if err := s.Delete("drop"); err != nil {
		t.Fatalf("删除失败：%v", err)
	}

	reloaded, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("重新加载失败：%v", err)
	}
	list, err := reloaded.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	if len(list) != 1 || list[0].ID != "keep" {
		t.Fatalf("删除结果未落盘：%+v", list)
	}
}

func TestFileStoreDeleteUnknownID(t *testing.T) {
	s := newStore(t)

	if err := s.Delete("missing"); !errors.Is(err, ErrNotFound) {
		t.Fatalf("期望 ErrNotFound，实际 %v", err)
	}
}

func TestFileStoreConcurrentCreateKeepsEveryIssue(t *testing.T) {
	path := filepath.Join(t.TempDir(), "data", "issues.json")
	s, err := NewFileStore(path)
	if err != nil {
		t.Fatalf("创建存储失败：%v", err)
	}

	const total = 40
	var wg sync.WaitGroup
	errs := make(chan error, total)
	for i := 0; i < total; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			if _, err := s.Create(issueFixture(fmt.Sprintf("id-%02d", i))); err != nil {
				errs <- err
			}
		}(i)
	}
	wg.Wait()
	close(errs)
	for err := range errs {
		t.Fatalf("并发创建失败：%v", err)
	}

	list, err := s.List()
	if err != nil {
		t.Fatalf("查询失败：%v", err)
	}
	if len(list) != total {
		t.Fatalf("期望 %d 条，实际 %d", total, len(list))
	}
}
