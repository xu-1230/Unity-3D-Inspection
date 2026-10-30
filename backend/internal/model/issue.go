package model

import (
	"math"
	"strings"
	"time"
)

// Priority 是问题优先级。
type Priority string

const (
	PriorityLow    Priority = "low"
	PriorityMedium Priority = "medium"
	PriorityHigh   Priority = "high"
)

// Status 是问题处理状态。
type Status string

const (
	StatusOpen       Status = "open"
	StatusInProgress Status = "in_progress"
	StatusResolved   Status = "resolved"
)

// 错误码，与需求规格说明书第 5.7 节一一对应。
const (
	CodeInvalidTitle    = "INVALID_TITLE"
	CodeInvalidPriority = "INVALID_PRIORITY"
	CodeInvalidStatus   = "INVALID_STATUS"
	CodeInvalidLocation = "INVALID_LOCATION"
)

// TimeLayout 是 RFC3339 的定宽毫秒格式，保证同一秒内的多次变更也能区分先后。
const TimeLayout = "2006-01-02T15:04:05.000Z07:00"

// FormatTime 把时间格式化为 UTC 的 RFC3339 字符串。
func FormatTime(t time.Time) string {
	return t.UTC().Format(TimeLayout)
}

// Location 是问题所在的世界坐标，服务端只存储与透传。
type Location struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// Valid 判断三个分量是否都是有限数值。
func (l Location) Valid() bool {
	for _, v := range []float64{l.X, l.Y, l.Z} {
		if math.IsNaN(v) || math.IsInf(v, 0) {
			return false
		}
	}
	return true
}

// Issue 是保存后的问题实体。
type Issue struct {
	ID          string   `json:"id"`
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    Priority `json:"priority"`
	Status      Status   `json:"status"`
	Location    Location `json:"location"`
	CreatedAt   string   `json:"createdAt"`
	UpdatedAt   string   `json:"updatedAt"`
}

// Error 是可直接映射为 HTTP 错误响应的校验错误。
type Error struct {
	Code    string
	Message string
	Field   string
}

func (e *Error) Error() string { return e.Message }

// CreateRequest 是创建接口的原始请求体，用指针区分「缺失」与「零值」。
type CreateRequest struct {
	Title       *string   `json:"title"`
	Description *string   `json:"description"`
	Priority    *string   `json:"priority"`
	Location    *Location `json:"location"`
}

// CreateValues 是通过校验后可直接落库的字段。
type CreateValues struct {
	Title       string
	Description string
	Priority    Priority
	Location    Location
}

// Validate 按「标题 → 优先级 → 坐标」的顺序校验，返回首个命中的错误。
func (r CreateRequest) Validate() (CreateValues, *Error) {
	var out CreateValues

	title := ""
	if r.Title != nil {
		title = strings.TrimSpace(*r.Title)
	}
	if title == "" {
		return out, &Error{Code: CodeInvalidTitle, Message: "标题不能为空", Field: "title"}
	}
	out.Title = title

	priority, ok := ParsePriority(r.Priority)
	if !ok {
		return out, &Error{
			Code:    CodeInvalidPriority,
			Message: "priority 必须为 low、medium 或 high 之一",
			Field:   "priority",
		}
	}
	out.Priority = priority

	if r.Location == nil || !r.Location.Valid() {
		return out, &Error{
			Code:    CodeInvalidLocation,
			Message: "location 必须包含 x、y、z 三个有限数值",
			Field:   "location",
		}
	}
	out.Location = *r.Location

	if r.Description != nil {
		out.Description = *r.Description
	}
	return out, nil
}

// StatusRequest 是修改状态接口的原始请求体。
type StatusRequest struct {
	Status *string `json:"status"`
}

// Validate 校验目标状态取值。
func (r StatusRequest) Validate() (Status, *Error) {
	status, ok := ParseStatus(r.Status)
	if !ok {
		return "", &Error{
			Code:    CodeInvalidStatus,
			Message: "status 必须为 open、in_progress 或 resolved 之一",
			Field:   "status",
		}
	}
	return status, nil
}

// ParsePriority 把字符串转为优先级枚举。
func ParsePriority(raw *string) (Priority, bool) {
	if raw == nil {
		return "", false
	}
	switch Priority(*raw) {
	case PriorityLow:
		return PriorityLow, true
	case PriorityMedium:
		return PriorityMedium, true
	case PriorityHigh:
		return PriorityHigh, true
	}
	return "", false
}

// ParseStatus 把字符串转为状态枚举。
func ParseStatus(raw *string) (Status, bool) {
	if raw == nil {
		return "", false
	}
	switch Status(*raw) {
	case StatusOpen:
		return StatusOpen, true
	case StatusInProgress:
		return StatusInProgress, true
	case StatusResolved:
		return StatusResolved, true
	}
	return "", false
}
