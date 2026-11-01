package api

// 本文件的用例只描述「当前 HTTP 层实际表现出来的行为」，不做任何应当性判断。
// 目的：在更换存储实现、改造容器与脚本之前，把契约钉死，任何偏移都会立刻失败。

import (
	"bytes"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"sort"
	"strings"
	"testing"
)

func doRequest(t *testing.T, srv *httptest.Server, method, path, body string, headers map[string]string) (*http.Response, []byte) {
	t.Helper()
	var reader io.Reader
	if body != "" {
		reader = strings.NewReader(body)
	}
	req, err := http.NewRequest(method, srv.URL+path, reader)
	if err != nil {
		t.Fatalf("构造请求失败：%v", err)
	}
	if body != "" {
		req.Header.Set("Content-Type", "application/json")
	}
	for k, v := range headers {
		req.Header.Set(k, v)
	}
	resp, err := srv.Client().Do(req)
	if err != nil {
		t.Fatalf("请求失败：%v", err)
	}
	defer resp.Body.Close()
	payload, err := io.ReadAll(resp.Body)
	if err != nil {
		t.Fatalf("读取响应失败：%v", err)
	}
	return resp, payload
}

// TestCharacterizationRoutingAndMethodMatrix 固定路由表、405 的 Allow 头与 404 的纯文本形态。
func TestCharacterizationRoutingAndMethodMatrix(t *testing.T) {
	tests := []struct {
		name      string
		method    string
		path      string
		wantCode  int
		wantAllow string
		wantCT    string
	}{
		{name: "列表不支持 PUT", method: http.MethodPut, path: "/api/issues", wantCode: http.StatusMethodNotAllowed, wantAllow: "GET, HEAD, POST", wantCT: "text/plain; charset=utf-8"},
		{name: "列表不支持 DELETE", method: http.MethodDelete, path: "/api/issues", wantCode: http.StatusMethodNotAllowed, wantAllow: "GET, HEAD, POST", wantCT: "text/plain; charset=utf-8"},
		{name: "单项不支持 GET", method: http.MethodGet, path: "/api/issues/abc", wantCode: http.StatusMethodNotAllowed, wantAllow: "DELETE", wantCT: "text/plain; charset=utf-8"},
		{name: "单项不支持 POST", method: http.MethodPost, path: "/api/issues/abc", wantCode: http.StatusMethodNotAllowed, wantAllow: "DELETE", wantCT: "text/plain; charset=utf-8"},
		{name: "状态路径不支持 GET", method: http.MethodGet, path: "/api/issues/abc/status", wantCode: http.StatusMethodNotAllowed, wantAllow: "PATCH", wantCT: "text/plain; charset=utf-8"},
		{name: "列表尾斜杠不匹配", method: http.MethodGet, path: "/api/issues/", wantCode: http.StatusNotFound, wantCT: "text/plain; charset=utf-8"},
		{name: "路径大小写敏感", method: http.MethodGet, path: "/API/health", wantCode: http.StatusNotFound, wantCT: "text/plain; charset=utf-8"},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)

			resp, payload := doRequest(t, srv, tc.method, tc.path, "", nil)

			if resp.StatusCode != tc.wantCode {
				t.Fatalf("期望 %d，实际 %d：%s", tc.wantCode, resp.StatusCode, payload)
			}
			if got := resp.Header.Get("Allow"); got != tc.wantAllow {
				t.Fatalf("Allow 头期望 %q，实际 %q", tc.wantAllow, got)
			}
			if got := resp.Header.Get("Content-Type"); got != tc.wantCT {
				t.Fatalf("Content-Type 期望 %q，实际 %q", tc.wantCT, got)
			}
			if tc.wantCode == http.StatusNotFound && !strings.Contains(string(payload), "404 page not found") {
				t.Fatalf("404 响应体应为标准纯文本，实际 %q", payload)
			}
		})
	}
}

// TestCharacterizationCleanPathRedirect 固定 ServeMux 对重复斜杠的 301 归一化。
func TestCharacterizationCleanPathRedirect(t *testing.T) {
	srv := newTestServer(t)
	client := srv.Client()
	client.CheckRedirect = func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }

	resp, err := client.Get(srv.URL + "/api//issues")
	if err != nil {
		t.Fatalf("请求失败：%v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusMovedPermanently {
		t.Fatalf("期望 301，实际 %d", resp.StatusCode)
	}
	if got := resp.Header.Get("Location"); got != "/api/issues" {
		t.Fatalf("归一化目标期望 /api/issues，实际 %q", got)
	}
}

// TestCharacterizationHeadOnHealth 固定 GET 模式同时接受 HEAD 且不写响应体。
func TestCharacterizationHeadOnHealth(t *testing.T) {
	srv := newTestServer(t)

	resp, payload := doRequest(t, srv, http.MethodHead, "/api/health", "", nil)

	if resp.StatusCode != http.StatusOK {
		t.Fatalf("期望 200，实际 %d", resp.StatusCode)
	}
	if len(payload) != 0 {
		t.Fatalf("HEAD 不应返回响应体，实际 %q", payload)
	}
	if got := resp.Header.Get("Content-Type"); got != "application/json; charset=utf-8" {
		t.Fatalf("Content-Type 不符：%q", got)
	}
}

// TestCharacterizationCreateBodyEdgeCases 固定解析层的宽松与严格之处。
func TestCharacterizationCreateBodyEdgeCases(t *testing.T) {
	tests := []struct {
		name     string
		body     string
		wantCode int
		wantErr  string
		check    func(t *testing.T, issue map[string]any)
	}{
		{name: "空请求体视为非法 JSON", body: "", wantCode: http.StatusBadRequest, wantErr: codeInvalidJSON},
		{name: "JSON null 落到字段缺省", body: `null`, wantCode: http.StatusBadRequest, wantErr: "INVALID_TITLE"},
		{name: "数组不是对象", body: `[]`, wantCode: http.StatusBadRequest, wantErr: codeInvalidJSON},
		{name: "裸字符串不是对象", body: `"x"`, wantCode: http.StatusBadRequest, wantErr: codeInvalidJSON},
		{name: "标题类型错视为非法 JSON", body: `{"title":1,"priority":"low","location":{"x":0,"y":0,"z":0}}`, wantCode: http.StatusBadRequest, wantErr: codeInvalidJSON},
		{name: "坐标数值溢出视为非法 JSON", body: `{"title":"ok","priority":"low","location":{"x":1e999,"y":0,"z":0}}`, wantCode: http.StatusBadRequest, wantErr: codeInvalidJSON},
		{name: "坐标类型错视为非法 JSON", body: `{"title":"ok","priority":"low","location":{"x":"1","y":0,"z":0}}`, wantCode: http.StatusBadRequest, wantErr: codeInvalidJSON},
		{name: "坐标为 null 是校验错误", body: `{"title":"ok","priority":"low","location":null}`, wantCode: http.StatusBadRequest, wantErr: "INVALID_LOCATION"},
		{
			name:     "未知字段被忽略",
			body:     `{"title":"ok","priority":"low","location":{"x":0,"y":0,"z":0},"zzz":1}`,
			wantCode: http.StatusCreated,
			check: func(t *testing.T, issue map[string]any) {
				if issue["title"] != "ok" {
					t.Fatalf("标题不符：%v", issue["title"])
				}
			},
		},
		{
			name:     "重复键后者生效",
			body:     `{"title":"first","title":"second","priority":"low","location":{"x":0,"y":0,"z":0}}`,
			wantCode: http.StatusCreated,
			check: func(t *testing.T, issue map[string]any) {
				if issue["title"] != "second" {
					t.Fatalf("重复键应取后者，实际 %v", issue["title"])
				}
			},
		},
		{
			name:     "对象之后的尾随数据被忽略",
			body:     `{"title":"x","priority":"low","location":{"x":0,"y":0,"z":0}}{"a":1}`,
			wantCode: http.StatusCreated,
			check: func(t *testing.T, issue map[string]any) {
				if issue["title"] != "x" {
					t.Fatalf("标题不符：%v", issue["title"])
				}
			},
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)

			resp, payload := doRequest(t, srv, http.MethodPost, "/api/issues", tc.body, nil)

			if resp.StatusCode != tc.wantCode {
				t.Fatalf("期望 %d，实际 %d：%s", tc.wantCode, resp.StatusCode, payload)
			}
			if tc.wantErr != "" {
				if detail := decodeError(t, payload); detail.Code != tc.wantErr {
					t.Fatalf("期望错误码 %s，实际 %s（%s）", tc.wantErr, detail.Code, payload)
				}
				return
			}
			var issue map[string]any
			if err := json.Unmarshal(payload, &issue); err != nil {
				t.Fatalf("解析响应失败：%v", err)
			}
			tc.check(t, issue)
		})
	}
}

// TestCharacterizationOversizedBodyRejected 固定超过 1MB 的请求体返回 400 而不是 413。
func TestCharacterizationOversizedBodyRejected(t *testing.T) {
	srv := newTestServer(t)
	body := `{"title":"` + strings.Repeat("a", 1<<21) + `","priority":"low","location":{"x":0,"y":0,"z":0}}`

	resp, payload := doRequest(t, srv, http.MethodPost, "/api/issues", body, nil)

	if resp.StatusCode != http.StatusBadRequest {
		t.Fatalf("期望 400，实际 %d", resp.StatusCode)
	}
	if detail := decodeError(t, payload); detail.Code != codeInvalidJSON {
		t.Fatalf("期望错误码 %s，实际 %s", codeInvalidJSON, detail.Code)
	}
}

// TestCharacterizationValidationOrder 固定「标题 → 优先级 → 坐标」的校验顺序。
func TestCharacterizationValidationOrder(t *testing.T) {
	tests := []struct {
		name string
		body string
		want string
	}{
		{name: "空对象先报标题", body: `{}`, want: "INVALID_TITLE"},
		{name: "缺优先级先于缺坐标", body: `{"title":"x"}`, want: "INVALID_PRIORITY"},
		{name: "优先级缺省先于坐标缺省", body: `{"title":"x","location":{"x":0,"y":0,"z":0}}`, want: "INVALID_PRIORITY"},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)

			resp, payload := doRequest(t, srv, http.MethodPost, "/api/issues", tc.body, nil)

			if resp.StatusCode != http.StatusBadRequest {
				t.Fatalf("期望 400，实际 %d：%s", resp.StatusCode, payload)
			}
			if detail := decodeError(t, payload); detail.Code != tc.want {
				t.Fatalf("期望错误码 %s，实际 %s", tc.want, detail.Code)
			}
		})
	}
}

// TestCharacterizationPatchValidatesBeforeLookup 固定 PATCH 先校验请求体、再查标识。
func TestCharacterizationPatchValidatesBeforeLookup(t *testing.T) {
	tests := []struct {
		name     string
		id       string
		body     string
		wantCode int
		wantErr  string
	}{
		{name: "标识不存在且状态非法报状态", id: "missing", body: `{"status":"done"}`, wantCode: http.StatusBadRequest, wantErr: "INVALID_STATUS"},
		{name: "标识不存在且状态缺失报状态", id: "missing", body: `{}`, wantCode: http.StatusBadRequest, wantErr: "INVALID_STATUS"},
		{name: "标识不存在但状态合法报 404", id: "missing", body: `{"status":"open"}`, wantCode: http.StatusNotFound, wantErr: codeNotFound},
		{name: "空请求体报非法 JSON", id: "missing", body: "", wantCode: http.StatusBadRequest, wantErr: codeInvalidJSON},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)

			resp, payload := doRequest(t, srv, http.MethodPatch, "/api/issues/"+tc.id+"/status", tc.body, nil)

			if resp.StatusCode != tc.wantCode {
				t.Fatalf("期望 %d，实际 %d：%s", tc.wantCode, resp.StatusCode, payload)
			}
			if detail := decodeError(t, payload); detail.Code != tc.wantErr {
				t.Fatalf("期望错误码 %s，实际 %s", tc.wantErr, detail.Code)
			}
		})
	}
}

// TestCharacterizationCORSCoverage 固定跨域放行的判定范围与响应头。
func TestCharacterizationCORSCoverage(t *testing.T) {
	tests := []struct {
		name     string
		origin   string
		wantACAO string
	}{
		{name: "本机带端口", origin: "http://localhost:5173", wantACAO: "http://localhost:5173"},
		{name: "回环地址", origin: "http://127.0.0.1:9999", wantACAO: "http://127.0.0.1:9999"},
		{name: "主机名大写", origin: "HTTP://LOCALHOST:1", wantACAO: "HTTP://LOCALHOST:1"},
		{name: "https 本机", origin: "https://localhost", wantACAO: "https://localhost"},
		{name: "本机无端口", origin: "http://localhost", wantACAO: "http://localhost"},
		{name: "IPv6 回环不放行", origin: "http://[::1]:8080", wantACAO: ""},
		{name: "null 来源不放行", origin: "null", wantACAO: ""},
		{name: "畸形来源不放行", origin: "://bad", wantACAO: ""},
		{name: "外部来源不放行", origin: "http://evil.example.com", wantACAO: ""},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)

			resp, payload := doRequest(t, srv, http.MethodGet, "/api/health", "", map[string]string{"Origin": tc.origin})

			if resp.StatusCode != http.StatusOK {
				t.Fatalf("跨域只影响响应头，不应拦截请求，实际 %d", resp.StatusCode)
			}
			if string(bytes.TrimSpace(payload)) != `{"status":"ok"}` {
				t.Fatalf("响应体不符：%s", payload)
			}
			if got := resp.Header.Get("Access-Control-Allow-Origin"); got != tc.wantACAO {
				t.Fatalf("Allow-Origin 期望 %q，实际 %q", tc.wantACAO, got)
			}
			wantVary := ""
			if tc.wantACAO != "" {
				wantVary = "Origin"
			}
			if got := resp.Header.Get("Vary"); got != wantVary {
				t.Fatalf("Vary 期望 %q，实际 %q", wantVary, got)
			}
		})
	}
}

// TestCharacterizationPreflightBypassesRouting 固定 OPTIONS 由中间件直接应答，不进入路由匹配。
func TestCharacterizationPreflightBypassesRouting(t *testing.T) {
	tests := []struct {
		name     string
		path     string
		origin   string
		wantACAO string
	}{
		{name: "未注册路径也返回 204", path: "/api/nope", origin: "", wantACAO: ""},
		{name: "已注册路径返回 204", path: "/api/issues", origin: "http://localhost:5173", wantACAO: "http://localhost:5173"},
		{name: "外部来源仍是 204 但不放行", path: "/api/issues", origin: "http://evil.example.com", wantACAO: ""},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			srv := newTestServer(t)
			headers := map[string]string{"Access-Control-Request-Method": "PATCH"}
			if tc.origin != "" {
				headers["Origin"] = tc.origin
			}

			resp, payload := doRequest(t, srv, http.MethodOptions, tc.path, "", headers)

			if resp.StatusCode != http.StatusNoContent {
				t.Fatalf("期望 204，实际 %d", resp.StatusCode)
			}
			if len(payload) != 0 {
				t.Fatalf("预检不应有响应体，实际 %q", payload)
			}
			if got := resp.Header.Get("Access-Control-Allow-Origin"); got != tc.wantACAO {
				t.Fatalf("Allow-Origin 期望 %q，实际 %q", tc.wantACAO, got)
			}
		})
	}
}

// TestCharacterizationCreateResponseShape 固定创建响应的字段集合，三端共用此契约。
func TestCharacterizationCreateResponseShape(t *testing.T) {
	srv := newTestServer(t)

	resp, payload := doRequest(t, srv, http.MethodPost, "/api/issues",
		`{"title":"机柜A顶部渗水","priority":"high","location":{"x":1.5,"y":2,"z":-3.25}}`, nil)
	if resp.StatusCode != http.StatusCreated {
		t.Fatalf("期望 201，实际 %d：%s", resp.StatusCode, payload)
	}

	var fields map[string]any
	if err := json.Unmarshal(payload, &fields); err != nil {
		t.Fatalf("解析响应失败：%v", err)
	}

	got := make([]string, 0, len(fields))
	for k := range fields {
		got = append(got, k)
	}
	sort.Strings(got)
	want := []string{"area", "createdAt", "description", "deviceId", "id", "location", "priority", "status", "title", "updatedAt"}
	if strings.Join(got, ",") != strings.Join(want, ",") {
		t.Fatalf("字段集合不符：期望 %v，实际 %v", want, got)
	}

	location, ok := fields["location"].(map[string]any)
	if !ok {
		t.Fatalf("location 应为对象：%v", fields["location"])
	}
	if len(location) != 3 || location["x"] == nil || location["y"] == nil || location["z"] == nil {
		t.Fatalf("location 应只含 x/y/z：%v", location)
	}
	if fields["deviceId"] != "" || fields["area"] != "" {
		t.Fatalf("缺省的设备与区域应序列化为空串：%v/%v", fields["deviceId"], fields["area"])
	}
}

// TestCharacterizationListIsBareArray 固定列表返回裸数组而非包装对象。
func TestCharacterizationListIsBareArray(t *testing.T) {
	srv := newTestServer(t)

	_, payload := doRequest(t, srv, http.MethodGet, "/api/issues", "", nil)
	if string(bytes.TrimSpace(payload)) != "[]" {
		t.Fatalf("空列表应为 []，实际 %s", payload)
	}

	createIssue(t, srv)

	_, payload = doRequest(t, srv, http.MethodGet, "/api/issues", "", nil)
	if !strings.HasPrefix(strings.TrimSpace(string(payload)), "[{") {
		t.Fatalf("列表应为对象数组，实际 %s", payload)
	}
}

// TestCharacterizationDeleteResponse 固定 204 无响应体、无 Content-Type，且查询串被忽略。
func TestCharacterizationDeleteResponse(t *testing.T) {
	srv := newTestServer(t)
	created := createIssue(t, srv)

	resp, payload := doRequest(t, srv, http.MethodDelete, "/api/issues/"+created.ID+"?trace=1", "", nil)

	if resp.StatusCode != http.StatusNoContent {
		t.Fatalf("期望 204，实际 %d：%s", resp.StatusCode, payload)
	}
	if len(payload) != 0 {
		t.Fatalf("204 不应有响应体，实际 %q", payload)
	}
	if got := resp.Header.Get("Content-Type"); got != "" {
		t.Fatalf("204 不应设置 Content-Type，实际 %q", got)
	}
}

// TestCharacterizationUpdateStatusResponse 固定改状态返回完整对象，且不改变创建时间。
func TestCharacterizationUpdateStatusResponse(t *testing.T) {
	srv := newTestServer(t)
	created := createIssue(t, srv)

	resp, payload := doRequest(t, srv, http.MethodPatch, "/api/issues/"+created.ID+"/status", `{"status":"in_progress"}`, nil)

	if resp.StatusCode != http.StatusOK {
		t.Fatalf("期望 200，实际 %d：%s", resp.StatusCode, payload)
	}
	if got := resp.Header.Get("Content-Type"); got != "application/json; charset=utf-8" {
		t.Fatalf("Content-Type 不符：%q", got)
	}
	updated := decodeIssue(t, payload)
	if updated.Status != "in_progress" {
		t.Fatalf("状态未更新：%s", updated.Status)
	}
	if updated.CreatedAt != created.CreatedAt {
		t.Fatalf("createdAt 被改动：%s -> %s", created.CreatedAt, updated.CreatedAt)
	}
	if updated.UpdatedAt < created.UpdatedAt {
		t.Fatalf("updatedAt 不应回退：%s -> %s", created.UpdatedAt, updated.UpdatedAt)
	}
	if updated.Title != created.Title || updated.Location != created.Location {
		t.Fatalf("其它字段被改动：%+v", updated)
	}
}

// TestCharacterizationIdentifiersAreOpaque 固定标识按不透明字符串处理，不做格式校验。
func TestCharacterizationIdentifiersAreOpaque(t *testing.T) {
	srv := newTestServer(t)

	resp, payload := doRequest(t, srv, http.MethodPatch, "/api/issues/not-a-real-id/status", `{"status":"open"}`, nil)
	if resp.StatusCode != http.StatusNotFound {
		t.Fatalf("任意非空标识都应走「不存在」分支，实际 %d：%s", resp.StatusCode, payload)
	}

	resp, payload = doRequest(t, srv, http.MethodDelete, "/api/issues/0123456789ABCDEF0123456789ABCDEF", "", nil)
	if resp.StatusCode != http.StatusNotFound {
		t.Fatalf("大写十六进制标识同样按不透明字符串处理，应返回 404，实际 %d：%s", resp.StatusCode, payload)
	}
}
