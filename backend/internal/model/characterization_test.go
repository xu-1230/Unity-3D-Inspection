package model

// 本文件固定模型层的现有行为：时间格式、字段归一化边界、校验顺序与错误码。
// 这些行为同时被 HTTP 层、Unity 客户端与 React 管理端依赖，改动必须是有意的。

import (
	"math"
	"strings"
	"testing"
	"time"
)

func strPtr(s string) *string { return &s }

func TestCharacterizationFormatTime(t *testing.T) {
	tests := []struct {
		name string
		at   time.Time
		want string
	}{
		{name: "整秒", at: time.Date(2026, 10, 30, 8, 12, 33, 0, time.UTC), want: "2026-10-30T08:12:33.000Z"},
		{name: "毫秒", at: time.Date(2026, 10, 30, 8, 12, 33, 123*int(time.Millisecond), time.UTC), want: "2026-10-30T08:12:33.123Z"},
		{name: "纳秒被截断不四舍五入", at: time.Date(2026, 10, 30, 8, 12, 33, 999999999, time.UTC), want: "2026-10-30T08:12:33.999Z"},
		{name: "不足一毫秒归零", at: time.Date(2026, 10, 30, 8, 12, 33, 999999, time.UTC), want: "2026-10-30T08:12:33.000Z"},
		{name: "非 UTC 时区转为 UTC", at: time.Date(2026, 10, 30, 16, 12, 33, 0, time.FixedZone("CST", 8*3600)), want: "2026-10-30T08:12:33.000Z"},
		{name: "零值时间", at: time.Time{}, want: "0001-01-01T00:00:00.000Z"},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			got := FormatTime(tc.at)
			if got != tc.want {
				t.Fatalf("期望 %q，实际 %q", tc.want, got)
			}
			if len(got) != 24 {
				t.Fatalf("时间字符串应定宽 24 字符，实际 %d", len(got))
			}
		})
	}
}

func TestCharacterizationTimeLayoutSortsLexicographically(t *testing.T) {
	earlier := FormatTime(time.Date(2026, 1, 2, 3, 4, 5, 6_000_000, time.UTC))
	later := FormatTime(time.Date(2026, 1, 2, 3, 4, 5, 7_000_000, time.UTC))

	if !(earlier < later) {
		t.Fatalf("字符串比较应与时间先后一致：%q vs %q", earlier, later)
	}
	parsed, err := time.Parse(TimeLayout, earlier)
	if err != nil {
		t.Fatalf("应能按 TimeLayout 解析：%v", err)
	}
	if FormatTime(parsed) != earlier {
		t.Fatalf("解析后再格式化应保持不变：%q -> %q", earlier, FormatTime(parsed))
	}
}

func TestCharacterizationLocationValid(t *testing.T) {
	tests := []struct {
		name string
		in   Location
		want bool
	}{
		{name: "零值", in: Location{}, want: true},
		{name: "负值", in: Location{X: -1, Y: -2.5, Z: -3}, want: true},
		{name: "极大有限值", in: Location{X: math.MaxFloat64}, want: true},
		{name: "X 为 NaN", in: Location{X: math.NaN()}, want: false},
		{name: "Y 为 NaN", in: Location{Y: math.NaN()}, want: false},
		{name: "Z 为 NaN", in: Location{Z: math.NaN()}, want: false},
		{name: "正无穷", in: Location{X: math.Inf(1)}, want: false},
		{name: "负无穷", in: Location{Y: math.Inf(-1)}, want: false},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			if got := tc.in.Valid(); got != tc.want {
				t.Fatalf("期望 %v，实际 %v", tc.want, got)
			}
		})
	}
}

func TestCharacterizationOptionalLabelBounds(t *testing.T) {
	tests := []struct {
		name  string
		input string
		want  string
	}{
		{name: "原样保留", input: "CB-101", want: "CB-101"},
		{name: "去除首尾空白", input: "  CB-101  ", want: "CB-101"},
		{name: "纯空白归一为空串", input: "   ", want: ""},
		{name: "恰好 64 个字符不截断", input: strings.Repeat("设", 64), want: strings.Repeat("设", 64)},
		{name: "65 个字符截断为 64", input: strings.Repeat("设", 65), want: strings.Repeat("设", 64)},
		{name: "超长内容截断为 64", input: strings.Repeat("A", 200), want: strings.Repeat("A", 64)},
		{name: "按字符而非字节计长", input: strings.Repeat("🙂", 65), want: strings.Repeat("🙂", 64)},
		{name: "先裁剪再计长", input: "  " + strings.Repeat("B", 64) + "  ", want: strings.Repeat("B", 64)},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			req := CreateRequest{
				Title:    strPtr("机柜A"),
				Priority: strPtr("high"),
				DeviceID: strPtr(tc.input),
				Location: &Location{},
			}

			values, err := req.Validate()
			if err != nil {
				t.Fatalf("未预期错误：%v", err)
			}
			if values.DeviceID != tc.want {
				t.Fatalf("deviceId 期望 %q（长度 %d），实际 %q（长度 %d）",
					tc.want, len([]rune(tc.want)), values.DeviceID, len([]rune(values.DeviceID)))
			}
		})
	}
}

func TestCharacterizationAreaFollowsSameRules(t *testing.T) {
	req := CreateRequest{
		Title:    strPtr("机柜A"),
		Priority: strPtr("low"),
		Area:     strPtr("  " + strings.Repeat("区", 70) + "  "),
		Location: &Location{},
	}

	values, err := req.Validate()
	if err != nil {
		t.Fatalf("未预期错误：%v", err)
	}
	if values.Area != strings.Repeat("区", 64) {
		t.Fatalf("area 应与 deviceId 同规则，实际 %q", values.Area)
	}
}

func TestCharacterizationTextFieldsAreNotNormalized(t *testing.T) {
	req := CreateRequest{
		Title:       strPtr("  标题两侧空白被去掉  "),
		Description: strPtr("  描述保留原样\n第二行\t结束  "),
		Priority:    strPtr("high"),
		Location:    &Location{},
	}

	values, err := req.Validate()
	if err != nil {
		t.Fatalf("未预期错误：%v", err)
	}
	if values.Title != "标题两侧空白被去掉" {
		t.Fatalf("标题应去除首尾空白，实际 %q", values.Title)
	}
	if values.Description != "  描述保留原样\n第二行\t结束  " {
		t.Fatalf("描述应原样保留，实际 %q", values.Description)
	}

	noDesc := CreateRequest{
		Title:    strPtr("机柜A"),
		Priority: strPtr("high"),
		Location: &Location{},
	}
	if got, _ := noDesc.Validate(); got.Description != "" {
		t.Fatalf("描述缺省应为空串，实际 %q", got.Description)
	}
}

func TestCharacterizationTitleHasNoLengthCap(t *testing.T) {
	long := strings.Repeat("长", 500)
	req := CreateRequest{
		Title:    strPtr(long),
		Priority: strPtr("high"),
		Location: &Location{},
	}

	values, err := req.Validate()
	if err != nil {
		t.Fatalf("未预期错误：%v", err)
	}
	if values.Title != long {
		t.Fatalf("标题不应被截断，长度 %d -> %d", len([]rune(long)), len([]rune(values.Title)))
	}
}

func TestCharacterizationErrorsAreStable(t *testing.T) {
	_, titleErr := CreateRequest{Priority: strPtr("high"), Location: &Location{}}.Validate()
	if titleErr.Code != CodeInvalidTitle || titleErr.Field != "title" || titleErr.Message != "标题不能为空" {
		t.Fatalf("标题错误不符：%+v", titleErr)
	}

	_, priorityErr := CreateRequest{Title: strPtr("x"), Location: &Location{}}.Validate()
	if priorityErr.Code != CodeInvalidPriority || priorityErr.Field != "priority" ||
		priorityErr.Message != "priority 必须为 low、medium 或 high 之一" {
		t.Fatalf("优先级错误不符：%+v", priorityErr)
	}

	_, locationErr := CreateRequest{Title: strPtr("x"), Priority: strPtr("high")}.Validate()
	if locationErr.Code != CodeInvalidLocation || locationErr.Field != "location" ||
		locationErr.Message != "location 必须包含 x、y、z 三个有限数值" {
		t.Fatalf("坐标错误不符：%+v", locationErr)
	}

	_, statusErr := StatusRequest{}.Validate()
	if statusErr.Code != CodeInvalidStatus || statusErr.Field != "status" ||
		statusErr.Message != "status 必须为 open、in_progress 或 resolved 之一" {
		t.Fatalf("状态错误不符：%+v", statusErr)
	}
}

func TestCharacterizationEnumParsingIsStrict(t *testing.T) {
	priorities := []struct {
		raw  string
		want Priority
		ok   bool
	}{
		{raw: "low", want: PriorityLow, ok: true},
		{raw: "medium", want: PriorityMedium, ok: true},
		{raw: "high", want: PriorityHigh, ok: true},
		{raw: "High", ok: false},
		{raw: " high", ok: false},
		{raw: "high ", ok: false},
		{raw: "", ok: false},
	}
	for _, tc := range priorities {
		got, ok := ParsePriority(strPtr(tc.raw))
		if ok != tc.ok || got != tc.want {
			t.Fatalf("ParsePriority(%q) = %q,%v，期望 %q,%v", tc.raw, got, ok, tc.want, tc.ok)
		}
	}
	if _, ok := ParsePriority(nil); ok {
		t.Fatal("缺失的优先级不应通过")
	}

	statuses := []struct {
		raw  string
		want Status
		ok   bool
	}{
		{raw: "open", want: StatusOpen, ok: true},
		{raw: "in_progress", want: StatusInProgress, ok: true},
		{raw: "resolved", want: StatusResolved, ok: true},
		{raw: "in-progress", ok: false},
		{raw: "OPEN", ok: false},
		{raw: " open", ok: false},
	}
	for _, tc := range statuses {
		got, ok := ParseStatus(strPtr(tc.raw))
		if ok != tc.ok || got != tc.want {
			t.Fatalf("ParseStatus(%q) = %q,%v，期望 %q,%v", tc.raw, got, ok, tc.want, tc.ok)
		}
	}
	if _, ok := ParseStatus(nil); ok {
		t.Fatal("缺失的状态不应通过")
	}
}
