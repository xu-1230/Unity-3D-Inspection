package store

import (
	"database/sql"
	"fmt"
	"os"
	"path/filepath"
	"sync"
	"time"

	_ "modernc.org/sqlite"

	"unity-3d-inspection/backend/internal/model"
)

// schema 是问题表的建表语句。
//
// 刻意不给 id 加唯一约束：历史实现允许重复标识追加为多条（接口层用随机 32 位标识，正常路径不会重复），
// 一致性套件已固定「重复标识只作用于最早一条」的语义，因此这里用自增的 seq 表达写入顺序。
const schema = `
CREATE TABLE IF NOT EXISTS issues (
	seq         INTEGER PRIMARY KEY AUTOINCREMENT,
	id          TEXT    NOT NULL,
	title       TEXT    NOT NULL,
	description TEXT    NOT NULL,
	priority    TEXT    NOT NULL,
	status      TEXT    NOT NULL,
	device_id   TEXT    NOT NULL,
	area        TEXT    NOT NULL,
	x           REAL    NOT NULL,
	y           REAL    NOT NULL,
	z           REAL    NOT NULL,
	created_at  TEXT    NOT NULL,
	updated_at  TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_issues_created_at ON issues (created_at DESC, id DESC);
`

// columns 是查询与插入共用的列顺序，避免两处顺序漂移。
const columns = "id, title, description, priority, status, device_id, area, x, y, z, created_at, updated_at"

// SQLiteStore 把问题保存到单个 SQLite 数据库文件。
type SQLiteStore struct {
	db  *sql.DB
	mu  sync.Mutex
	now func() time.Time
}

// NewSQLiteStore 打开（必要时创建）数据库并确保表结构就绪。
func NewSQLiteStore(path string) (*SQLiteStore, error) {
	if dir := filepath.Dir(path); dir != "" && dir != "." {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return nil, err
		}
	}

	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, err
	}

	// 单连接：SQLite 同一时刻只允许一个写者，串行化后无需处理 SQLITE_BUSY 抖动。
	db.SetMaxOpenConns(1)

	for _, pragma := range []string{
		"PRAGMA journal_mode = WAL",
		"PRAGMA busy_timeout = 5000",
		"PRAGMA synchronous = NORMAL",
	} {
		if _, err := db.Exec(pragma); err != nil {
			db.Close()
			return nil, fmt.Errorf("%s: %w", pragma, err)
		}
	}

	if _, err := db.Exec(schema); err != nil {
		db.Close()
		return nil, err
	}

	return &SQLiteStore{db: db, now: time.Now}, nil
}

// Close 释放数据库连接。
func (s *SQLiteStore) Close() error { return s.db.Close() }

// List 返回按创建时间倒序的问题副本；时间相同时按标识倒序，再按写入顺序正序。
func (s *SQLiteStore) List() ([]model.Issue, error) {
	rows, err := s.db.Query(
		"SELECT " + columns + " FROM issues ORDER BY created_at DESC, id DESC, seq ASC")
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	out := []model.Issue{}
	for rows.Next() {
		issue, err := scanIssue(rows)
		if err != nil {
			return nil, err
		}
		out = append(out, issue)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	return out, nil
}

// Create 追加问题，创建时间与更新时间取服务端当前时间。
func (s *SQLiteStore) Create(issue model.Issue) (model.Issue, error) {
	stamp := model.FormatTime(s.now())
	issue.CreatedAt = stamp
	issue.UpdatedAt = stamp

	if _, err := s.insert(s.db, issue); err != nil {
		return model.Issue{}, err
	}
	return issue, nil
}

// UpdateStatus 修改最早写入的那条问题并刷新更新时间。
func (s *SQLiteStore) UpdateStatus(id string, status model.Status) (model.Issue, error) {
	stamp := model.FormatTime(s.now())

	s.mu.Lock()
	defer s.mu.Unlock()

	res, err := s.db.Exec(
		"UPDATE issues SET status = ?, updated_at = ? WHERE seq = (SELECT seq FROM issues WHERE id = ? ORDER BY seq LIMIT 1)",
		string(status), stamp, id)
	if err != nil {
		return model.Issue{}, err
	}
	affected, err := res.RowsAffected()
	if err != nil {
		return model.Issue{}, err
	}
	if affected == 0 {
		return model.Issue{}, ErrNotFound
	}

	row := s.db.QueryRow("SELECT "+columns+" FROM issues WHERE id = ? ORDER BY seq LIMIT 1", id)
	return scanIssue(row)
}

// Delete 移除最早写入的那条问题。
func (s *SQLiteStore) Delete(id string) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	res, err := s.db.Exec(
		"DELETE FROM issues WHERE seq = (SELECT seq FROM issues WHERE id = ? ORDER BY seq LIMIT 1)", id)
	if err != nil {
		return err
	}
	affected, err := res.RowsAffected()
	if err != nil {
		return err
	}
	if affected == 0 {
		return ErrNotFound
	}
	return nil
}

// Import 按原样写入一批问题（保留传入的标识与时间），用于一次性迁移旧数据。
// 标识与创建时间都相同的记录视为已导入而跳过，因此可以重复执行。
func (s *SQLiteStore) Import(issues []model.Issue) (inserted, skipped int, err error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	tx, err := s.db.Begin()
	if err != nil {
		return 0, 0, err
	}
	defer func() {
		if err != nil {
			tx.Rollback()
		}
	}()

	for _, issue := range issues {
		var exists int
		checkErr := tx.QueryRow(
			"SELECT COUNT(1) FROM issues WHERE id = ? AND created_at = ?", issue.ID, issue.CreatedAt).Scan(&exists)
		if checkErr != nil {
			return inserted, skipped, checkErr
		}
		if exists > 0 {
			skipped++
			continue
		}
		if _, execErr := s.insert(tx, issue); execErr != nil {
			return inserted, skipped, execErr
		}
		inserted++
	}

	if err = tx.Commit(); err != nil {
		return 0, 0, err
	}
	return inserted, skipped, nil
}

// execer 让插入语句在连接与事务上复用。
type execer interface {
	Exec(query string, args ...any) (sql.Result, error)
}

func (s *SQLiteStore) insert(e execer, issue model.Issue) (sql.Result, error) {
	return e.Exec(
		"INSERT INTO issues ("+columns+") VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
		issue.ID, issue.Title, issue.Description, string(issue.Priority), string(issue.Status),
		issue.DeviceID, issue.Area, issue.Location.X, issue.Location.Y, issue.Location.Z,
		issue.CreatedAt, issue.UpdatedAt)
}

// rowScanner 同时适配 *sql.Row 与 *sql.Rows。
type rowScanner interface {
	Scan(dest ...any) error
}

func scanIssue(row rowScanner) (model.Issue, error) {
	var (
		issue                model.Issue
		priority, status     string
		createdAt, updatedAt string
		location             model.Location
	)

	err := row.Scan(
		&issue.ID, &issue.Title, &issue.Description, &priority, &status,
		&issue.DeviceID, &issue.Area, &location.X, &location.Y, &location.Z,
		&createdAt, &updatedAt)
	if err != nil {
		return model.Issue{}, err
	}

	issue.Priority = model.Priority(priority)
	issue.Status = model.Status(status)
	issue.Location = location
	issue.CreatedAt = createdAt
	issue.UpdatedAt = updatedAt
	return issue, nil
}
