package store

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"sync"
	"time"

	"inspection-board/backend/internal/model"
)

var ErrNotFound = errors.New("issue not found")

type JSONStore struct {
	mu     sync.RWMutex
	path   string
	issues []model.Issue
}

func OpenJSON(path string) (*JSONStore, error) {
	store := &JSONStore{path: path, issues: make([]model.Issue, 0)}
	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return store, nil
	}
	if err != nil {
		return nil, fmt.Errorf("read issue store: %w", err)
	}
	if len(data) == 0 {
		return store, nil
	}
	if err := json.Unmarshal(data, &store.issues); err != nil {
		return nil, fmt.Errorf("decode issue store: %w", err)
	}
	if store.issues == nil {
		store.issues = make([]model.Issue, 0)
	}
	return store, nil
}

func (store *JSONStore) List() []model.Issue {
	store.mu.RLock()
	defer store.mu.RUnlock()

	result := make([]model.Issue, len(store.issues))
	for index, issue := range store.issues {
		result[len(store.issues)-1-index] = issue
	}
	return result
}

func (store *JSONStore) Create(request model.CreateIssueRequest) (model.Issue, error) {
	store.mu.Lock()
	defer store.mu.Unlock()

	now := time.Now().UTC().Format(time.RFC3339Nano)
	issue := model.Issue{
		ID:          newID(),
		Title:       request.Title,
		Description: request.Description,
		Priority:    request.Priority,
		Status:      model.StatusOpen,
		Position:    request.Position,
		CreatedAt:   now,
		UpdatedAt:   now,
	}
	store.issues = append(store.issues, issue)
	if err := store.saveLocked(); err != nil {
		store.issues = store.issues[:len(store.issues)-1]
		return model.Issue{}, err
	}
	return issue, nil
}

func (store *JSONStore) UpdateStatus(id, status string) (model.Issue, error) {
	store.mu.Lock()
	defer store.mu.Unlock()

	for index := range store.issues {
		if store.issues[index].ID != id {
			continue
		}
		previous := store.issues[index]
		store.issues[index].Status = status
		store.issues[index].UpdatedAt = time.Now().UTC().Format(time.RFC3339Nano)
		if err := store.saveLocked(); err != nil {
			store.issues[index] = previous
			return model.Issue{}, err
		}
		return store.issues[index], nil
	}
	return model.Issue{}, ErrNotFound
}

func (store *JSONStore) saveLocked() error {
	if err := os.MkdirAll(filepath.Dir(store.path), 0o755); err != nil {
		return fmt.Errorf("create data directory: %w", err)
	}
	data, err := json.MarshalIndent(store.issues, "", "  ")
	if err != nil {
		return fmt.Errorf("encode issue store: %w", err)
	}
	data = append(data, '\n')

	temporary, err := os.CreateTemp(filepath.Dir(store.path), ".issues-*.tmp")
	if err != nil {
		return fmt.Errorf("create temporary issue store: %w", err)
	}
	temporaryPath := temporary.Name()
	defer os.Remove(temporaryPath)

	if _, err := temporary.Write(data); err != nil {
		temporary.Close()
		return fmt.Errorf("write temporary issue store: %w", err)
	}
	if err := temporary.Sync(); err != nil {
		temporary.Close()
		return fmt.Errorf("sync temporary issue store: %w", err)
	}
	if err := temporary.Close(); err != nil {
		return fmt.Errorf("close temporary issue store: %w", err)
	}
	if err := os.Rename(temporaryPath, store.path); err != nil {
		return fmt.Errorf("replace issue store: %w", err)
	}
	return nil
}

func newID() string {
	random := make([]byte, 16)
	if _, err := rand.Read(random); err != nil {
		return fmt.Sprintf("issue-%d", time.Now().UnixNano())
	}
	random[6] = (random[6] & 0x0f) | 0x40
	random[8] = (random[8] & 0x3f) | 0x80
	encoded := hex.EncodeToString(random)
	return fmt.Sprintf("%s-%s-%s-%s-%s", encoded[0:8], encoded[8:12], encoded[12:16], encoded[16:20], encoded[20:32])
}
