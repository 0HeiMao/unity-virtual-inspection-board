package model

import "strings"

const (
	PriorityLow    = "low"
	PriorityMedium = "medium"
	PriorityHigh   = "high"

	StatusOpen       = "open"
	StatusInProgress = "in_progress"
	StatusResolved   = "resolved"

	// MaxAreaLength 与 MaxAssetLength 限制区域名和设备名长度，两个字段都是可选的归属信息。
	MaxAreaLength  = 64
	MaxAssetLength = 64
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
	Area        string   `json:"area"`
	Asset       string   `json:"asset"`
	Position    Position `json:"position"`
	CreatedAt   string   `json:"createdAt"`
	UpdatedAt   string   `json:"updatedAt"`
	// DeletedAt 为空表示问题仍在巡检队列里；非空表示已归档（软删除），可通过恢复接口回到队列。
	DeletedAt string `json:"deletedAt,omitempty"`
}

type CreateIssueRequest struct {
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    string   `json:"priority"`
	Area        string   `json:"area"`
	Asset       string   `json:"asset"`
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
	request.Area = strings.TrimSpace(request.Area)
	request.Asset = strings.TrimSpace(request.Asset)
}
