package model

import "strings"

const (
	PriorityLow    = "low"
	PriorityMedium = "medium"
	PriorityHigh   = "high"

	StatusOpen       = "open"
	StatusInProgress = "in_progress"
	StatusResolved   = "resolved"
)

type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

type Issue struct {
	ID          string   `json:"id"`
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    string   `json:"priority"`
	Status      string   `json:"status"`
	Position    Position `json:"position"`
	CreatedAt   string   `json:"createdAt"`
	UpdatedAt   string   `json:"updatedAt"`
}

type CreateIssueRequest struct {
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    string   `json:"priority"`
	Position    Position `json:"position"`
}

type UpdateStatusRequest struct {
	Status string `json:"status"`
}

func IsValidPriority(value string) bool {
	switch value {
	case PriorityLow, PriorityMedium, PriorityHigh:
		return true
	default:
		return false
	}
}

func IsValidStatus(value string) bool {
	switch value {
	case StatusOpen, StatusInProgress, StatusResolved:
		return true
	default:
		return false
	}
}

func (request *CreateIssueRequest) Normalize() {
	request.Title = strings.TrimSpace(request.Title)
	request.Description = strings.TrimSpace(request.Description)
	request.Priority = strings.TrimSpace(request.Priority)
}
