package model

import (
	"encoding/hex"
	"math"
	"testing"
)

func ptr(s string) *string { return &s }

func validCreate() CreateRequest {
	return CreateRequest{
		Title:    ptr("机柜A顶部渗水"),
		Priority: ptr("high"),
		Location: &Location{X: 1.5, Y: 2, Z: -3.25},
	}
}

func TestCreateRequestValidate(t *testing.T) {
	tests := []struct {
		name      string
		mutate    func(*CreateRequest)
		wantCode  string
		wantField string
	}{
		{name: "合法请求", mutate: func(*CreateRequest) {}},
		{name: "标题缺失", mutate: func(r *CreateRequest) { r.Title = nil }, wantCode: CodeInvalidTitle, wantField: "title"},
		{name: "标题为空白", mutate: func(r *CreateRequest) { r.Title = ptr("   ") }, wantCode: CodeInvalidTitle, wantField: "title"},
		{name: "优先级缺失", mutate: func(r *CreateRequest) { r.Priority = nil }, wantCode: CodeInvalidPriority, wantField: "priority"},
		{name: "优先级非法", mutate: func(r *CreateRequest) { r.Priority = ptr("urgent") }, wantCode: CodeInvalidPriority, wantField: "priority"},
		{name: "坐标缺失", mutate: func(r *CreateRequest) { r.Location = nil }, wantCode: CodeInvalidLocation, wantField: "location"},
		{name: "坐标含 NaN", mutate: func(r *CreateRequest) {
			r.Location = &Location{X: math.NaN(), Y: 1, Z: 1}
		}, wantCode: CodeInvalidLocation, wantField: "location"},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			req := validCreate()
			tc.mutate(&req)

			values, err := req.Validate()

			if tc.wantCode == "" {
				if err != nil {
					t.Fatalf("期望通过校验，实际返回错误 %v", err)
				}
				if values.Title != "机柜A顶部渗水" || values.Priority != PriorityHigh {
					t.Fatalf("校验结果字段不符：%+v", values)
				}
				return
			}
			if err == nil {
				t.Fatal("期望返回校验错误，实际通过")
			}
			if err.Code != tc.wantCode || err.Field != tc.wantField {
				t.Fatalf("错误码或字段不符：code=%s field=%s", err.Code, err.Field)
			}
			if err.Message == "" {
				t.Fatal("错误信息为空")
			}
		})
	}
}

func TestCreateRequestValidateTrimsTitle(t *testing.T) {
	req := validCreate()
	req.Title = ptr("  机柜B漏水  ")

	values, err := req.Validate()

	if err != nil {
		t.Fatalf("未预期错误：%v", err)
	}
	if values.Title != "机柜B漏水" {
		t.Fatalf("标题未被去除首尾空白：%q", values.Title)
	}
}

func TestCreateRequestValidateDefaultsDescription(t *testing.T) {
	req := validCreate()

	values, err := req.Validate()

	if err != nil {
		t.Fatalf("未预期错误：%v", err)
	}
	if values.Description != "" {
		t.Fatalf("描述缺省值应为空串，实际 %q", values.Description)
	}
}

func TestStatusRequestValidate(t *testing.T) {
	tests := []struct {
		name     string
		raw      *string
		want     Status
		wantCode string
	}{
		{name: "open", raw: ptr("open"), want: StatusOpen},
		{name: "in_progress", raw: ptr("in_progress"), want: StatusInProgress},
		{name: "resolved", raw: ptr("resolved"), want: StatusResolved},
		{name: "大写字面量非法", raw: ptr("Resolved"), wantCode: CodeInvalidStatus},
		{name: "空串非法", raw: ptr(""), wantCode: CodeInvalidStatus},
		{name: "缺失非法", raw: nil, wantCode: CodeInvalidStatus},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			status, err := StatusRequest{Status: tc.raw}.Validate()

			if tc.wantCode != "" {
				if err == nil || err.Code != tc.wantCode {
					t.Fatalf("期望错误码 %s，实际 %v", tc.wantCode, err)
				}
				if err.Field != "status" {
					t.Fatalf("错误字段应为 status，实际 %q", err.Field)
				}
				return
			}
			if err != nil {
				t.Fatalf("未预期错误：%v", err)
			}
			if status != tc.want {
				t.Fatalf("期望 %s，实际 %s", tc.want, status)
			}
		})
	}
}

func TestNewIDIsUniqueAndHex(t *testing.T) {
	seen := make(map[string]struct{}, 500)
	for i := 0; i < 500; i++ {
		id, err := NewID()
		if err != nil {
			t.Fatalf("生成标识失败：%v", err)
		}
		if len(id) != 32 {
			t.Fatalf("标识长度应为 32，实际 %d（%s）", len(id), id)
		}
		if _, err := hex.DecodeString(id); err != nil {
			t.Fatalf("标识不是十六进制：%s", id)
		}
		if _, dup := seen[id]; dup {
			t.Fatalf("标识重复：%s", id)
		}
		seen[id] = struct{}{}
	}
}
