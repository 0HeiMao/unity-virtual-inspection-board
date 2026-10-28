package httpapi

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"

	"inspection-board/backend/internal/model"
	"inspection-board/backend/internal/store"
)

func TestIssueLifecycleAndPersistence(t *testing.T) {
	dataPath := filepath.Join(t.TempDir(), "issues.json")
	issues, err := store.OpenJSON(dataPath)
	if err != nil {
		t.Fatal(err)
	}
	handler := New(issues, log.New(io.Discard, "", 0))

	createBody := `{
		"title": " 1 号机柜温度异常 ",
		"description": "右侧温度偏高",
		"priority": "high",
		"position": {"x": 1.25, "y": 0.8, "z": 2.4}
	}`
	createResponse := perform(handler, http.MethodPost, "/api/issues", createBody)
	if createResponse.Code != http.StatusCreated {
		t.Fatalf("expected 201, got %d: %s", createResponse.Code, createResponse.Body.String())
	}
	var created model.Issue
	decodeResponse(t, createResponse, &created)
	if created.ID == "" || created.Title != "1 号机柜温度异常" || created.Status != model.StatusOpen {
		t.Fatalf("unexpected created issue: %+v", created)
	}
	if created.CreatedAt == "" || created.UpdatedAt == "" {
		t.Fatalf("timestamps were not generated: %+v", created)
	}

	listResponse := perform(handler, http.MethodGet, "/api/issues", "")
	if listResponse.Code != http.StatusOK {
		t.Fatalf("expected 200, got %d", listResponse.Code)
	}
	var listPayload struct {
		Issues []model.Issue `json:"issues"`
	}
	decodeResponse(t, listResponse, &listPayload)
	if len(listPayload.Issues) != 1 {
		t.Fatalf("expected one issue, got %+v", listPayload.Issues)
	}

	updateResponse := perform(handler, http.MethodPatch, "/api/issues/"+created.ID+"/status", `{"status":"in_progress"}`)
	if updateResponse.Code != http.StatusOK {
		t.Fatalf("expected 200, got %d: %s", updateResponse.Code, updateResponse.Body.String())
	}
	var updated model.Issue
	decodeResponse(t, updateResponse, &updated)
	if updated.Status != model.StatusInProgress {
		t.Fatalf("expected in_progress, got %q", updated.Status)
	}

	reloaded, err := store.OpenJSON(dataPath)
	if err != nil {
		t.Fatal(err)
	}
	persisted := reloaded.List()
	if len(persisted) != 1 || persisted[0].Status != model.StatusInProgress {
		t.Fatalf("status did not persist: %+v", persisted)
	}
}

func TestValidationErrorsAreExplicit(t *testing.T) {
	issues, err := store.OpenJSON(filepath.Join(t.TempDir(), "issues.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := New(issues, log.New(io.Discard, "", 0))

	missingTitle := perform(handler, http.MethodPost, "/api/issues", `{
		"title": "   ",
		"priority": "high",
		"position": {"x": 0, "y": 0, "z": 0}
	}`)
	assertError(t, missingTitle, http.StatusBadRequest, "title 不能为空")

	invalidPriority := perform(handler, http.MethodPost, "/api/issues", `{
		"title": "测试",
		"priority": "urgent",
		"position": {"x": 0, "y": 0, "z": 0}
	}`)
	assertError(t, invalidPriority, http.StatusBadRequest, "priority 必须是 low、medium 或 high")

	invalidStatus := perform(handler, http.MethodPatch, "/api/issues/missing/status", `{"status":"closed"}`)
	assertError(t, invalidStatus, http.StatusBadRequest, "status 必须是 open、in_progress 或 resolved")

	tooLongArea := perform(handler, http.MethodPost, "/api/issues", fmt.Sprintf(`{
		"title": "测试",
		"priority": "low",
		"area": %q,
		"position": {"x": 0, "y": 0, "z": 0}
	}`, strings.Repeat("区", model.MaxAreaLength+1)))
	assertError(t, tooLongArea, http.StatusBadRequest, "area 不能超过 64 个字符")
}

func TestCreateKeepsOwnershipAndArchiveRestoresIssue(t *testing.T) {
	dataPath := filepath.Join(t.TempDir(), "issues.json")
	issues, err := store.OpenJSON(dataPath)
	if err != nil {
		t.Fatal(err)
	}
	handler := New(issues, log.New(io.Discard, "", 0))

	createResponse := perform(handler, http.MethodPost, "/api/issues", `{
		"title": "2 号机柜风扇异响",
		"priority": "medium",
		"area": " A 区 · 设备巡检 ",
		"asset": " 2 号机柜 ",
		"position": {"x": 0.5, "y": 1.2, "z": 4.4}
	}`)
	if createResponse.Code != http.StatusCreated {
		t.Fatalf("expected 201, got %d: %s", createResponse.Code, createResponse.Body.String())
	}
	var created model.Issue
	decodeResponse(t, createResponse, &created)
	if created.Area != "A 区 · 设备巡检" || created.Asset != "2 号机柜" {
		t.Fatalf("ownership fields were not trimmed or stored: %+v", created)
	}

	deleteResponse := perform(handler, http.MethodDelete, "/api/issues/"+created.ID, "")
	if deleteResponse.Code != http.StatusNoContent {
		t.Fatalf("expected 204, got %d: %s", deleteResponse.Code, deleteResponse.Body.String())
	}

	// 归档后不再出现在巡检队列，但会出现在回收站里。
	active := listIssues(t, handler, "/api/issues")
	if len(active) != 0 {
		t.Fatalf("archived issue still listed as active: %+v", active)
	}
	archived := listIssues(t, handler, "/api/issues?deleted=1")
	if len(archived) != 1 || archived[0].ID != created.ID || archived[0].DeletedAt == "" {
		t.Fatalf("archived issue missing from recycle bin: %+v", archived)
	}

	// 已归档的问题不允许再改状态。
	statusAfterArchive := perform(handler, http.MethodPatch, "/api/issues/"+created.ID+"/status", `{"status":"resolved"}`)
	assertError(t, statusAfterArchive, http.StatusNotFound, "问题不存在")

	reloaded, err := store.OpenJSON(dataPath)
	if err != nil {
		t.Fatal(err)
	}
	if len(reloaded.List()) != 1 {
		t.Fatalf("archived issue should still be persisted for restore: %+v", reloaded.List())
	}

	restoreResponse := perform(handler, http.MethodPost, "/api/issues/"+created.ID+"/restore", "")
	if restoreResponse.Code != http.StatusOK {
		t.Fatalf("expected 200, got %d: %s", restoreResponse.Code, restoreResponse.Body.String())
	}
	var restored model.Issue
	decodeResponse(t, restoreResponse, &restored)
	if restored.ID != created.ID || restored.DeletedAt != "" {
		t.Fatalf("restore did not clear deletedAt: %+v", restored)
	}
	if active := listIssues(t, handler, "/api/issues"); len(active) != 1 {
		t.Fatalf("restored issue missing from active list: %+v", active)
	}

	missing := perform(handler, http.MethodPost, "/api/issues/missing/restore", "")
	assertError(t, missing, http.StatusNotFound, "问题不存在")
}

func listIssues(t *testing.T, handler http.Handler, path string) []model.Issue {
	t.Helper()
	response := perform(handler, http.MethodGet, path, "")
	if response.Code != http.StatusOK {
		t.Fatalf("expected 200 from %s, got %d: %s", path, response.Code, response.Body.String())
	}
	var payload struct {
		Issues []model.Issue `json:"issues"`
	}
	decodeResponse(t, response, &payload)
	return payload.Issues
}

func TestHealthAndCORS(t *testing.T) {
	issues, err := store.OpenJSON(filepath.Join(t.TempDir(), "issues.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := New(issues, log.New(io.Discard, "", 0))

	health := perform(handler, http.MethodGet, "/health", "")
	if health.Code != http.StatusOK {
		t.Fatalf("expected 200, got %d", health.Code)
	}

	preflight := perform(handler, http.MethodOptions, "/api/issues", "")
	if preflight.Code != http.StatusNoContent {
		t.Fatalf("expected 204, got %d", preflight.Code)
	}
	if preflight.Header().Get("Access-Control-Allow-Origin") != "*" {
		t.Fatal("missing CORS allow-origin header")
	}
}

func perform(handler http.Handler, method, path, body string) *httptest.ResponseRecorder {
	request := httptest.NewRequest(method, path, bytes.NewBufferString(body))
	if body != "" {
		request.Header.Set("Content-Type", "application/json")
	}
	response := httptest.NewRecorder()
	handler.ServeHTTP(response, request)
	return response
}

func decodeResponse(t *testing.T, response *httptest.ResponseRecorder, target any) {
	t.Helper()
	if err := json.NewDecoder(response.Body).Decode(target); err != nil {
		t.Fatal(err)
	}
}

func assertError(t *testing.T, response *httptest.ResponseRecorder, status int, message string) {
	t.Helper()
	if response.Code != status {
		t.Fatalf("expected %d, got %d: %s", status, response.Code, response.Body.String())
	}
	var payload struct {
		Error string `json:"error"`
	}
	decodeResponse(t, response, &payload)
	if payload.Error != message {
		t.Fatalf("expected error %q, got %q", message, payload.Error)
	}
}
