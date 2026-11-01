package api

import (
	"bytes"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"testing"

	"unity-3d-inspection/backend/internal/model"
	"unity-3d-inspection/backend/internal/store"
)

func newTestServer(t *testing.T) *httptest.Server {
	t.Helper()
	st, err := store.NewFileStore(filepath.Join(t.TempDir(), "issues.json"))
	if err != nil {
		t.Fatalf("创建存储失败：%v", err)
	}
	srv := httptest.NewServer(NewRouter(st))
	t.Cleanup(srv.Close)
	return srv
}

func call(t *testing.T, srv *httptest.Server, method, path, body string) (*http.Response, []byte) {
	t.Helper()
	var reader io.Reader
	if body != "" {
		reader = bytes.NewBufferString(body)
	}
	req, err := http.NewRequest(method, srv.URL+path, reader)
	if err != nil {
		t.Fatalf("构造请求失败：%v", err)
	}
	if body != "" {
		req.Header.Set("Content-Type", "application/json")
	}
	resp, err := srv.Client().Do(req)
	if err != nil {
		t.Fatalf("请求失败：%v", err)
	}
	t.Cleanup(func() { resp.Body.Close() })
	payload, err := io.ReadAll(resp.Body)
	if err != nil {
		t.Fatalf("读取响应失败：%v", err)
	}
	return resp, payload
}

func decodeIssue(t *testing.T, payload []byte) model.Issue {
	t.Helper()
	var issue model.Issue
	if err := json.Unmarshal(payload, &issue); err != nil {
		t.Fatalf("解析问题对象失败：%v，原文 %s", err, payload)
	}
	return issue
}

func decodeError(t *testing.T, payload []byte) errorDetail {
	t.Helper()
	var envelope errorEnvelope
	if err := json.Unmarshal(payload, &envelope); err != nil {
		t.Fatalf("解析错误响应失败：%v，原文 %s", err, payload)
	}
	return envelope.Error
}

func createIssue(t *testing.T, srv *httptest.Server) model.Issue {
	t.Helper()
	body := `{"title":"机柜A顶部渗水","description":"柜顶有水渍","priority":"high","location":{"x":1.5,"y":2,"z":-3.25}}`
	resp, payload := call(t, srv, http.MethodPost, "/api/issues", body)
	if resp.StatusCode != http.StatusCreated {
		t.Fatalf("期望 201，实际 %d：%s", resp.StatusCode, payload)
	}
	return decodeIssue(t, payload)
}

func TestHealth(t *testing.T) {
	srv := newTestServer(t)

	resp, payload := call(t, srv, http.MethodGet, "/api/health", "")

	if resp.StatusCode != http.StatusOK {
		t.Fatalf("期望 200，实际 %d", resp.StatusCode)
	}
	if string(bytes.TrimSpace(payload)) != `{"status":"ok"}` {
		t.Fatalf("响应体不符：%s", payload)
	}
}

func TestCreateIssueReturnsServerOwnedFields(t *testing.T) {
	srv := newTestServer(t)

	created := createIssue(t, srv)

	if len(created.ID) != 32 {
		t.Fatalf("标识应为 32 位十六进制，实际 %q", created.ID)
	}
	if created.Status != model.StatusOpen {
		t.Fatalf("新问题状态应为 open，实际 %s", created.Status)
	}
	if created.Title != "机柜A顶部渗水" || created.Description != "柜顶有水渍" {
		t.Fatalf("文本字段丢失：%+v", created)
	}
	if created.Location.X != 1.5 || created.Location.Z != -3.25 {
		t.Fatalf("坐标未原样保存：%+v", created.Location)
	}
	if created.CreatedAt == "" || created.UpdatedAt != created.CreatedAt {
		t.Fatalf("时间字段不符：%+v", created)
	}
}

func TestCreateIssueIgnoresClientSuppliedServerFields(t *testing.T) {
	srv := newTestServer(t)

	body := `{"id":"hack","status":"resolved","createdAt":"2000-01-01T00:00:00Z","title":"柜B漏水","priority":"low","location":{"x":0,"y":0,"z":0}}`
	resp, payload := call(t, srv, http.MethodPost, "/api/issues", body)
	if resp.StatusCode != http.StatusCreated {
		t.Fatalf("期望 201，实际 %d：%s", resp.StatusCode, payload)
	}

	created := decodeIssue(t, payload)
	if created.ID == "hack" {
		t.Fatal("客户端传入的 id 被采纳")
	}
	if created.Status != model.StatusOpen {
		t.Fatalf("客户端传入的 status 被采纳：%s", created.Status)
	}
	if created.CreatedAt == "2000-01-01T00:00:00Z" {
		t.Fatal("客户端传入的 createdAt 被采纳")
	}
}

func TestCreateIssueRejectsInvalidInput(t *testing.T) {
	tests := []struct {
		name      string
		body      string
		wantCode  string
		wantField string
	}{
		{
			name:      "标题缺失",
			body:      `{"priority":"high","location":{"x":1,"y":2,"z":3}}`,
			wantCode:  model.CodeInvalidTitle,
			wantField: "title",
		},
		{
			name:      "标题为空白",
			body:      `{"title":"  ","priority":"high","location":{"x":1,"y":2,"z":3}}`,
			wantCode:  model.CodeInvalidTitle,
			wantField: "title",
		},
		{
			name:      "优先级非法",
			body:      `{"title":"柜A","priority":"urgent","location":{"x":1,"y":2,"z":3}}`,
			wantCode:  model.CodeInvalidPriority,
			wantField: "priority",
		},
		{
			name:      "坐标缺失",
			body:      `{"title":"柜A","priority":"high"}`,
			wantCode:  model.CodeInvalidLocation,
			wantField: "location",
		},
		{
			name:     "JSON 非法",
			body:     `{"title":`,
			wantCode: codeInvalidJSON,
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)

			resp, payload := call(t, srv, http.MethodPost, "/api/issues", tc.body)

			if resp.StatusCode != http.StatusBadRequest {
				t.Fatalf("期望 400，实际 %d：%s", resp.StatusCode, payload)
			}
			detail := decodeError(t, payload)
			if detail.Code != tc.wantCode {
				t.Fatalf("期望错误码 %s，实际 %s", tc.wantCode, detail.Code)
			}
			if detail.Field != tc.wantField {
				t.Fatalf("期望字段 %q，实际 %q", tc.wantField, detail.Field)
			}
			if detail.Message == "" {
				t.Fatal("错误信息为空")
			}
		})
	}
}

func TestListIssuesReturnsSavedItems(t *testing.T) {
	srv := newTestServer(t)

	resp, payload := call(t, srv, http.MethodGet, "/api/issues", "")
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("空列表期望 200，实际 %d", resp.StatusCode)
	}
	if string(bytes.TrimSpace(payload)) != "[]" {
		t.Fatalf("空列表应返回 []，实际 %s", payload)
	}

	first := createIssue(t, srv)
	second := createIssue(t, srv)

	_, payload = call(t, srv, http.MethodGet, "/api/issues", "")
	var issues []model.Issue
	if err := json.Unmarshal(payload, &issues); err != nil {
		t.Fatalf("解析列表失败：%v", err)
	}
	if len(issues) != 2 {
		t.Fatalf("期望 2 条，实际 %d", len(issues))
	}
	ids := map[string]bool{issues[0].ID: true, issues[1].ID: true}
	if !ids[first.ID] || !ids[second.ID] {
		t.Fatalf("列表缺少已创建的问题：%+v", issues)
	}
}

func TestUpdateStatusChangesState(t *testing.T) {
	srv := newTestServer(t)
	created := createIssue(t, srv)

	resp, payload := call(t, srv, http.MethodPatch, "/api/issues/"+created.ID+"/status", `{"status":"resolved"}`)
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("期望 200，实际 %d：%s", resp.StatusCode, payload)
	}
	updated := decodeIssue(t, payload)
	if updated.Status != model.StatusResolved {
		t.Fatalf("状态未更新：%s", updated.Status)
	}
	if updated.CreatedAt != created.CreatedAt {
		t.Fatalf("createdAt 被改动：%s -> %s", created.CreatedAt, updated.CreatedAt)
	}

	_, payload = call(t, srv, http.MethodGet, "/api/issues", "")
	var issues []model.Issue
	if err := json.Unmarshal(payload, &issues); err != nil {
		t.Fatalf("解析列表失败：%v", err)
	}
	if len(issues) != 1 || issues[0].Status != model.StatusResolved {
		t.Fatalf("列表未反映新状态：%+v", issues)
	}
}

func TestUpdateStatusErrors(t *testing.T) {
	tests := []struct {
		name       string
		id         string
		body       string
		wantStatus int
		wantCode   string
	}{
		{name: "标识不存在", id: "missing", body: `{"status":"open"}`, wantStatus: http.StatusNotFound, wantCode: codeNotFound},
		{name: "状态非法", id: "any", body: `{"status":"done"}`, wantStatus: http.StatusBadRequest, wantCode: model.CodeInvalidStatus},
		{name: "状态缺失", id: "any", body: `{}`, wantStatus: http.StatusBadRequest, wantCode: model.CodeInvalidStatus},
		{name: "JSON 非法", id: "any", body: `{`, wantStatus: http.StatusBadRequest, wantCode: codeInvalidJSON},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)

			resp, payload := call(t, srv, http.MethodPatch, "/api/issues/"+tc.id+"/status", tc.body)

			if resp.StatusCode != tc.wantStatus {
				t.Fatalf("期望 %d，实际 %d：%s", tc.wantStatus, resp.StatusCode, payload)
			}
			if detail := decodeError(t, payload); detail.Code != tc.wantCode {
				t.Fatalf("期望错误码 %s，实际 %s", tc.wantCode, detail.Code)
			}
		})
	}
}

func TestCreateIssueKeepsOptionalDeviceAndArea(t *testing.T) {
	srv := newTestServer(t)

	body := `{"title":"柜A顶部渗水","priority":"medium","deviceId":"  CB-101  ","area":"A区 1F","location":{"x":0,"y":1,"z":0}}`
	resp, payload := call(t, srv, http.MethodPost, "/api/issues", body)
	if resp.StatusCode != http.StatusCreated {
		t.Fatalf("期望 201，实际 %d：%s", resp.StatusCode, payload)
	}

	created := decodeIssue(t, payload)
	if created.DeviceID != "CB-101" || created.Area != "A区 1F" {
		t.Fatalf("设备与区域字段未按预期保存：deviceId=%q area=%q", created.DeviceID, created.Area)
	}

	resp, payload = call(t, srv, http.MethodPost, "/api/issues",
		`{"title":"未标注设备","priority":"low","location":{"x":0,"y":0,"z":0}}`)
	if resp.StatusCode != http.StatusCreated {
		t.Fatalf("缺省设备字段不应导致失败，实际 %d：%s", resp.StatusCode, payload)
	}
	if bare := decodeIssue(t, payload); bare.DeviceID != "" || bare.Area != "" {
		t.Fatalf("缺省时应为空串：deviceId=%q area=%q", bare.DeviceID, bare.Area)
	}
}

func TestDeleteIssueRemovesIt(t *testing.T) {
	srv := newTestServer(t)
	created := createIssue(t, srv)

	resp, payload := call(t, srv, http.MethodDelete, "/api/issues/"+created.ID, "")
	if resp.StatusCode != http.StatusNoContent {
		t.Fatalf("期望 204，实际 %d：%s", resp.StatusCode, payload)
	}

	_, payload = call(t, srv, http.MethodGet, "/api/issues", "")
	if string(bytes.TrimSpace(payload)) != "[]" {
		t.Fatalf("删除后列表应为空，实际 %s", payload)
	}
}

func TestDeleteIssueUnknownID(t *testing.T) {
	srv := newTestServer(t)

	resp, payload := call(t, srv, http.MethodDelete, "/api/issues/missing", "")
	if resp.StatusCode != http.StatusNotFound {
		t.Fatalf("期望 404，实际 %d：%s", resp.StatusCode, payload)
	}
	if detail := decodeError(t, payload); detail.Code != codeNotFound {
		t.Fatalf("期望错误码 %s，实际 %s", codeNotFound, detail.Code)
	}
}

func TestCORSAllowsLocalOrigin(t *testing.T) {
	srv := newTestServer(t)

	req, err := http.NewRequest(http.MethodOptions, srv.URL+"/api/issues", nil)
	if err != nil {
		t.Fatalf("构造请求失败：%v", err)
	}
	req.Header.Set("Origin", "http://localhost:5173")
	req.Header.Set("Access-Control-Request-Method", "PATCH")

	resp, err := srv.Client().Do(req)
	if err != nil {
		t.Fatalf("请求失败：%v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusNoContent {
		t.Fatalf("预检期望 204，实际 %d", resp.StatusCode)
	}
	if got := resp.Header.Get("Access-Control-Allow-Origin"); got != "http://localhost:5173" {
		t.Fatalf("允许来源头不符：%q", got)
	}
	if got := resp.Header.Get("Access-Control-Allow-Methods"); got == "" {
		t.Fatal("缺少允许方法头")
	}
}

func TestCORSRejectsForeignOrigin(t *testing.T) {
	srv := newTestServer(t)

	req, err := http.NewRequest(http.MethodGet, srv.URL+"/api/health", nil)
	if err != nil {
		t.Fatalf("构造请求失败：%v", err)
	}
	req.Header.Set("Origin", "http://evil.example.com")

	resp, err := srv.Client().Do(req)
	if err != nil {
		t.Fatalf("请求失败：%v", err)
	}
	defer resp.Body.Close()

	if got := resp.Header.Get("Access-Control-Allow-Origin"); got != "" {
		t.Fatalf("外部来源不应获得跨域许可，实际 %q", got)
	}
}
