package main

import (
	"context"
	"errors"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"unity-3d-inspection/backend/internal/api"
	"unity-3d-inspection/backend/internal/store"
)

func main() {
	port := os.Getenv("PORT")
	if port == "" {
		port = "8080"
	}
	dataPath := os.Getenv("DATA_PATH")
	if dataPath == "" {
		dataPath = "data/issues.db"
	}

	st, err := store.NewSQLiteStore(dataPath)
	if err != nil {
		log.Fatalf("打开存储失败（%s）: %v", dataPath, err)
	}
	defer st.Close()

	srv := &http.Server{
		Addr:              ":" + port,
		Handler:           api.NewRouter(st),
		ReadHeaderTimeout: 5 * time.Second,
		ReadTimeout:       15 * time.Second,
		WriteTimeout:      15 * time.Second,
		IdleTimeout:       60 * time.Second,
	}

	errCh := make(chan error, 1)
	go func() {
		log.Printf("backend listening on http://localhost:%s (data=%s)", port, dataPath)
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			errCh <- err
		}
	}()

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	select {
	case err := <-errCh:
		log.Fatalf("服务异常退出: %v", err)
	case <-ctx.Done():
		log.Println("收到退出信号，正在关闭服务")
	}

	shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if err := srv.Shutdown(shutdownCtx); err != nil {
		log.Printf("关闭失败: %v", err)
	}
	log.Println("backend stopped")
}
