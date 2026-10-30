package store

import (
	"encoding/json"
	"os"
	"path/filepath"
	"sort"
	"sync"
	"time"

	"unity-3d-inspection/backend/internal/model"
)

// FileStore 把问题以单个 JSON 文件保存，写入采用临时文件加原子重命名。
type FileStore struct {
	mu     sync.Mutex
	path   string
	issues []model.Issue
	now    func() time.Time
}

// NewFileStore 加载已有数据；文件不存在时以空集合启动。
func NewFileStore(path string) (*FileStore, error) {
	s := &FileStore{path: path, issues: []model.Issue{}, now: time.Now}
	if err := s.load(); err != nil {
		return nil, err
	}
	return s, nil
}

func (s *FileStore) load() error {
	data, err := os.ReadFile(s.path)
	if err != nil {
		if os.IsNotExist(err) {
			return nil
		}
		return err
	}
	if len(data) == 0 {
		return nil
	}

	var loaded []model.Issue
	if err := json.Unmarshal(data, &loaded); err != nil {
		return err
	}
	if loaded != nil {
		s.issues = loaded
	}
	return nil
}

// List 返回按创建时间倒序的问题副本。
func (s *FileStore) List() ([]model.Issue, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	out := make([]model.Issue, len(s.issues))
	copy(out, s.issues)
	sort.SliceStable(out, func(i, j int) bool {
		if out[i].CreatedAt != out[j].CreatedAt {
			return out[i].CreatedAt > out[j].CreatedAt
		}
		return out[i].ID > out[j].ID
	})
	return out, nil
}

// Create 追加问题并落盘，创建时间与更新时间取服务端当前时间。
func (s *FileStore) Create(issue model.Issue) (model.Issue, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	stamp := model.FormatTime(s.now())
	issue.CreatedAt = stamp
	issue.UpdatedAt = stamp

	s.issues = append(s.issues, issue)
	if err := s.saveLocked(); err != nil {
		s.issues = s.issues[:len(s.issues)-1]
		return model.Issue{}, err
	}
	return issue, nil
}

// UpdateStatus 修改状态并刷新更新时间。
func (s *FileStore) UpdateStatus(id string, status model.Status) (model.Issue, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	for i := range s.issues {
		if s.issues[i].ID != id {
			continue
		}
		previous := s.issues[i]
		s.issues[i].Status = status
		s.issues[i].UpdatedAt = model.FormatTime(s.now())
		if err := s.saveLocked(); err != nil {
			s.issues[i] = previous
			return model.Issue{}, err
		}
		return s.issues[i], nil
	}
	return model.Issue{}, ErrNotFound
}

// saveLocked 先写临时文件再原子重命名，避免半写损坏。调用方须持有锁。
func (s *FileStore) saveLocked() error {
	dir := filepath.Dir(s.path)
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return err
	}

	data, err := json.MarshalIndent(s.issues, "", "  ")
	if err != nil {
		return err
	}

	tmp, err := os.CreateTemp(dir, ".issues-*.tmp")
	if err != nil {
		return err
	}
	tmpName := tmp.Name()
	defer os.Remove(tmpName)

	if _, err := tmp.Write(data); err != nil {
		tmp.Close()
		return err
	}
	if err := tmp.Close(); err != nil {
		return err
	}
	return os.Rename(tmpName, s.path)
}
