package store

import (
	"errors"
	"path/filepath"
	"testing"

	"inspection-board/backend/internal/model"
)

func TestJSONStorePersistsIssues(t *testing.T) {
	path := filepath.Join(t.TempDir(), "nested", "issues.json")
	store, err := OpenJSON(path)
	if err != nil {
		t.Fatal(err)
	}

	created, err := store.Create(model.CreateIssueRequest{
		Title:       "1 号机柜温度异常",
		Description: "右侧温度偏高",
		Priority:    model.PriorityHigh,
		Position:    model.Position{X: 1.25, Y: 0.8, Z: 2.4},
	})
	if err != nil {
		t.Fatal(err)
	}
	if created.ID == "" || created.Status != model.StatusOpen || created.CreatedAt == "" {
		t.Fatalf("unexpected created issue: %+v", created)
	}

	updated, err := store.UpdateStatus(created.ID, model.StatusResolved)
	if err != nil {
		t.Fatal(err)
	}
	if updated.Status != model.StatusResolved {
		t.Fatalf("expected resolved, got %q", updated.Status)
	}

	reloaded, err := OpenJSON(path)
	if err != nil {
		t.Fatal(err)
	}
	issues := reloaded.List()
	if len(issues) != 1 || issues[0].ID != created.ID || issues[0].Status != model.StatusResolved {
		t.Fatalf("issue did not survive reload: %+v", issues)
	}
}

func TestUpdateUnknownIssue(t *testing.T) {
	store, err := OpenJSON(filepath.Join(t.TempDir(), "issues.json"))
	if err != nil {
		t.Fatal(err)
	}
	_, err = store.UpdateStatus("missing", model.StatusOpen)
	if !errors.Is(err, ErrNotFound) {
		t.Fatalf("expected ErrNotFound, got %v", err)
	}
}
