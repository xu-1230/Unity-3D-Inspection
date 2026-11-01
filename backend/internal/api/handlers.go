package api

import (
	"encoding/json"
	"errors"
	"log"
	"net/http"

	"unity-3d-inspection/backend/internal/model"
	"unity-3d-inspection/backend/internal/store"
)

// 与需求规格说明书 5.8 节保持一致，另补两个传输层错误码。
const (
	codeInvalidJSON = "INVALID_JSON"
	codeNotFound    = "ISSUE_NOT_FOUND"
	codeInternal    = "INTERNAL"
)

const maxBodyBytes = 1 << 20

type errorDetail struct {
	Code    string `json:"code"`
	Message string `json:"message"`
	Field   string `json:"field"`
}

type errorEnvelope struct {
	Error errorDetail `json:"error"`
}

func writeJSON(w http.ResponseWriter, status int, payload any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(payload); err != nil {
		log.Printf("写响应失败: %v", err)
	}
}

func writeError(w http.ResponseWriter, status int, code, message, field string) {
	writeJSON(w, status, errorEnvelope{Error: errorDetail{Code: code, Message: message, Field: field}})
}

func handleHealth(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func handleCreate(st store.Store) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		r.Body = http.MaxBytesReader(w, r.Body, maxBodyBytes)

		var req model.CreateRequest
		decoder := json.NewDecoder(r.Body)
		if err := decoder.Decode(&req); err != nil {
			writeError(w, http.StatusBadRequest, codeInvalidJSON, "请求体不是合法的 JSON 对象", "")
			return
		}

		values, verr := req.Validate()
		if verr != nil {
			writeError(w, http.StatusBadRequest, verr.Code, verr.Message, verr.Field)
			return
		}

		id, err := model.NewID()
		if err != nil {
			log.Printf("生成标识失败: %v", err)
			writeError(w, http.StatusInternalServerError, codeInternal, "服务内部错误", "")
			return
		}

		created, err := st.Create(model.Issue{
			ID:          id,
			Title:       values.Title,
			Description: values.Description,
			Priority:    values.Priority,
			Status:      model.StatusOpen,
			DeviceID:    values.DeviceID,
			Area:        values.Area,
			Location:    values.Location,
		})
		if err != nil {
			log.Printf("保存问题失败: %v", err)
			writeError(w, http.StatusInternalServerError, codeInternal, "服务内部错误", "")
			return
		}

		log.Printf("%s %s -> 201 issue=%s", r.Method, r.URL.Path, created.ID)
		writeJSON(w, http.StatusCreated, created)
	}
}

func handleList(st store.Store) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		issues, err := st.List()
		if err != nil {
			log.Printf("读取问题列表失败: %v", err)
			writeError(w, http.StatusInternalServerError, codeInternal, "服务内部错误", "")
			return
		}
		if issues == nil {
			issues = []model.Issue{}
		}
		writeJSON(w, http.StatusOK, issues)
	}
}

func handleUpdateStatus(st store.Store) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		r.Body = http.MaxBytesReader(w, r.Body, maxBodyBytes)

		id := r.PathValue("id")
		if id == "" {
			writeError(w, http.StatusNotFound, codeNotFound, "指定标识的问题不存在", "")
			return
		}

		var req model.StatusRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeError(w, http.StatusBadRequest, codeInvalidJSON, "请求体不是合法的 JSON 对象", "")
			return
		}

		status, verr := req.Validate()
		if verr != nil {
			writeError(w, http.StatusBadRequest, verr.Code, verr.Message, verr.Field)
			return
		}

		updated, err := st.UpdateStatus(id, status)
		if err != nil {
			if errors.Is(err, store.ErrNotFound) {
				writeError(w, http.StatusNotFound, codeNotFound, "指定标识的问题不存在", "")
				return
			}
			log.Printf("更新状态失败: %v", err)
			writeError(w, http.StatusInternalServerError, codeInternal, "服务内部错误", "")
			return
		}

		log.Printf("%s %s -> 200 issue=%s status=%s", r.Method, r.URL.Path, updated.ID, updated.Status)
		writeJSON(w, http.StatusOK, updated)
	}
}

func handleDelete(st store.Store) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		id := r.PathValue("id")
		if id == "" {
			writeError(w, http.StatusNotFound, codeNotFound, "指定标识的问题不存在", "")
			return
		}

		if err := st.Delete(id); err != nil {
			if errors.Is(err, store.ErrNotFound) {
				writeError(w, http.StatusNotFound, codeNotFound, "指定标识的问题不存在", "")
				return
			}
			log.Printf("删除问题失败: %v", err)
			writeError(w, http.StatusInternalServerError, codeInternal, "服务内部错误", "")
			return
		}

		log.Printf("%s %s -> 204 issue=%s", r.Method, r.URL.Path, id)
		w.WriteHeader(http.StatusNoContent)
	}
}
