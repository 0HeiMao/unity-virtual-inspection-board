package httpapi

import (
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log"
	"net/http"
	"strings"
	"time"

	"inspection-board/backend/internal/model"
	"inspection-board/backend/internal/store"
)

type Server struct {
	issues *store.JSONStore
	logger *log.Logger
}

func New(issues *store.JSONStore, logger *log.Logger) http.Handler {
	server := &Server{issues: issues, logger: logger}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", server.handleHealth)
	mux.HandleFunc("GET /api/issues", server.handleListIssues)
	mux.HandleFunc("POST /api/issues", server.handleCreateIssue)
	mux.HandleFunc("PATCH /api/issues/{id}/status", server.handleUpdateStatus)
	mux.HandleFunc("DELETE /api/issues/{id}", server.handleDeleteIssue)
	mux.HandleFunc("POST /api/issues/{id}/restore", server.handleRestoreIssue)
	return withCORS(withRequestLog(mux, logger))
}

func (server *Server) handleHealth(writer http.ResponseWriter, _ *http.Request) {
	writeJSON(writer, http.StatusOK, map[string]any{
		"status": "ok",
		"time":   time.Now().UTC().Format(time.RFC3339),
	})
}

// handleListIssues 默认只返回巡检队列里的问题；?deleted=1 返回回收站（已归档）里的问题。
func (server *Server) handleListIssues(writer http.ResponseWriter, request *http.Request) {
	wantDeleted := request.URL.Query().Get("deleted") == "1"
	all := server.issues.List()
	issues := make([]model.Issue, 0, len(all))
	for _, issue := range all {
		if (issue.DeletedAt != "") == wantDeleted {
			issues = append(issues, issue)
		}
	}
	writeJSON(writer, http.StatusOK, map[string]any{"issues": issues})
}

func (server *Server) handleCreateIssue(writer http.ResponseWriter, request *http.Request) {
	var input model.CreateIssueRequest
	if err := decodeJSON(writer, request, &input); err != nil {
		writeError(writer, http.StatusBadRequest, err.Error())
		return
	}
	input.Normalize()
	if input.Title == "" {
		writeError(writer, http.StatusBadRequest, "title 不能为空")
		return
	}
	if len([]rune(input.Title)) > 120 {
		writeError(writer, http.StatusBadRequest, "title 不能超过 120 个字符")
		return
	}
	if !model.IsValidPriority(input.Priority) {
		writeError(writer, http.StatusBadRequest, "priority 必须是 low、medium 或 high")
		return
	}
	if len([]rune(input.Description)) > 2000 {
		writeError(writer, http.StatusBadRequest, "description 不能超过 2000 个字符")
		return
	}
	if len([]rune(input.Area)) > model.MaxAreaLength {
		writeError(writer, http.StatusBadRequest, "area 不能超过 64 个字符")
		return
	}
	if len([]rune(input.Asset)) > model.MaxAssetLength {
		writeError(writer, http.StatusBadRequest, "asset 不能超过 64 个字符")
		return
	}

	issue, err := server.issues.Create(input)
	if err != nil {
		server.logger.Printf("create issue: %v", err)
		writeError(writer, http.StatusInternalServerError, "保存问题失败")
		return
	}
	writeJSON(writer, http.StatusCreated, issue)
}

func (server *Server) handleUpdateStatus(writer http.ResponseWriter, request *http.Request) {
	var input model.UpdateStatusRequest
	if err := decodeJSON(writer, request, &input); err != nil {
		writeError(writer, http.StatusBadRequest, err.Error())
		return
	}
	input.Status = strings.TrimSpace(input.Status)
	if !model.IsValidStatus(input.Status) {
		writeError(writer, http.StatusBadRequest, "status 必须是 open、in_progress 或 resolved")
		return
	}

	issue, err := server.issues.UpdateStatus(request.PathValue("id"), input.Status)
	if errors.Is(err, store.ErrNotFound) {
		writeError(writer, http.StatusNotFound, "问题不存在")
		return
	}
	if err != nil {
		server.logger.Printf("update issue status: %v", err)
		writeError(writer, http.StatusInternalServerError, "更新状态失败")
		return
	}
	writeJSON(writer, http.StatusOK, issue)
}

func (server *Server) handleDeleteIssue(writer http.ResponseWriter, request *http.Request) {
	if err := server.issues.Delete(request.PathValue("id")); errors.Is(err, store.ErrNotFound) {
		writeError(writer, http.StatusNotFound, "问题不存在")
		return
	} else if err != nil {
		server.logger.Printf("delete issue: %v", err)
		writeError(writer, http.StatusInternalServerError, "删除问题失败")
		return
	}
	writer.WriteHeader(http.StatusNoContent)
}

func (server *Server) handleRestoreIssue(writer http.ResponseWriter, request *http.Request) {
	issue, err := server.issues.Restore(request.PathValue("id"))
	if errors.Is(err, store.ErrNotFound) {
		writeError(writer, http.StatusNotFound, "问题不存在")
		return
	}
	if err != nil {
		server.logger.Printf("restore issue: %v", err)
		writeError(writer, http.StatusInternalServerError, "恢复问题失败")
		return
	}
	writeJSON(writer, http.StatusOK, issue)
}

func decodeJSON(writer http.ResponseWriter, request *http.Request, target any) error {
	request.Body = http.MaxBytesReader(writer, request.Body, 1<<20)
	decoder := json.NewDecoder(request.Body)
	if err := decoder.Decode(target); err != nil {
		if errors.Is(err, io.EOF) {
			return errors.New("请求体不能为空")
		}
		return errors.New("请求体不是有效的 JSON")
	}
	if err := decoder.Decode(&struct{}{}); !errors.Is(err, io.EOF) {
		return errors.New("请求体只能包含一个 JSON 对象")
	}
	return nil
}

func writeJSON(writer http.ResponseWriter, status int, value any) {
	writer.Header().Set("Content-Type", "application/json; charset=utf-8")
	writer.WriteHeader(status)
	if err := json.NewEncoder(writer).Encode(value); err != nil {
		log.Printf("write response: %v", err)
	}
}

func writeError(writer http.ResponseWriter, status int, message string) {
	writeJSON(writer, status, map[string]string{"error": message})
}

func withCORS(next http.Handler) http.Handler {
	return http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		writer.Header().Set("Access-Control-Allow-Origin", "*")
		writer.Header().Set("Access-Control-Allow-Headers", "Content-Type")
		writer.Header().Set("Access-Control-Allow-Methods", "GET, POST, PATCH, DELETE, OPTIONS")
		if request.Method == http.MethodOptions {
			writer.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(writer, request)
	})
}

func withRequestLog(next http.Handler, logger *log.Logger) http.Handler {
	return http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		started := time.Now()
		next.ServeHTTP(writer, request)
		logger.Printf("%s %s %s", request.Method, request.URL.Path, time.Since(started).Round(time.Millisecond))
	})
}

func (server *Server) String() string {
	return fmt.Sprintf("inspection API with %d issues", len(server.issues.List()))
}
