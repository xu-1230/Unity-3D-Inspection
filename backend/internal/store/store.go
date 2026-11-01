package store

import (
	"errors"

	"unity-3d-inspection/backend/internal/model"
)

// ErrNotFound 表示按标识查找的问题不存在。
var ErrNotFound = errors.New("issue not found")

// Store 是问题实体的持久化接口，当前实现为 SQLite，具体行为由 storetest 一致性套件固定。
type Store interface {
	// List 返回全部问题，按创建时间倒序。
	List() ([]model.Issue, error)
	// Create 保存新问题，补全创建时间与更新时间。
	Create(issue model.Issue) (model.Issue, error)
	// UpdateStatus 修改状态并刷新更新时间，标识不存在时返回 ErrNotFound。
	UpdateStatus(id string, status model.Status) (model.Issue, error)
	// Delete 移除指定问题，标识不存在时返回 ErrNotFound。
	Delete(id string) error
}
