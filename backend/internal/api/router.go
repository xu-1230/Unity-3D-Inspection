package api

import (
	"net/http"

	"unity-3d-inspection/backend/internal/store"
)

// NewRouter 注册全部接口并套上跨域中间件。
func NewRouter(st store.Store) http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/health", handleHealth)
	mux.HandleFunc("POST /api/issues", handleCreate(st))
	mux.HandleFunc("GET /api/issues", handleList(st))
	mux.HandleFunc("PATCH /api/issues/{id}/status", handleUpdateStatus(st))
	return withCORS(mux)
}
