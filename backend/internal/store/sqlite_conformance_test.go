package store_test

import (
	"path/filepath"
	"testing"

	"unity-3d-inspection/backend/internal/store"
	"unity-3d-inspection/backend/internal/store/storetest"
)

// TestSQLiteStoreConformance 用同一套一致性用例固定 SQLite 实现，
// 与 JSON 文件实现共用这批断言，任何一侧单独漂移都会失败。
func TestSQLiteStoreConformance(t *testing.T) {
	storetest.Run(t, storetest.Factory{
		Open: func(t *testing.T) (store.Store, func() store.Store) {
			t.Helper()
			path := filepath.Join(t.TempDir(), "data", "issues.db")

			first, err := store.NewSQLiteStore(path)
			if err != nil {
				t.Fatalf("打开数据库失败：%v", err)
			}
			t.Cleanup(func() { first.Close() })

			reopen := func() store.Store {
				next, err := store.NewSQLiteStore(path)
				if err != nil {
					t.Fatalf("重新打开数据库失败：%v", err)
				}
				t.Cleanup(func() { next.Close() })
				return next
			}
			return first, reopen
		},
	})
}
