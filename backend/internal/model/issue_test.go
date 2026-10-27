package model

import "testing"

func TestEnumValidation(t *testing.T) {
	for _, value := range []string{PriorityLow, PriorityMedium, PriorityHigh} {
		if !IsValidPriority(value) {
			t.Fatalf("expected %q to be a valid priority", value)
		}
	}
	if IsValidPriority("urgent") {
		t.Fatal("urgent must not be a valid priority")
	}

	for _, value := range []string{StatusOpen, StatusInProgress, StatusResolved} {
		if !IsValidStatus(value) {
			t.Fatalf("expected %q to be a valid status", value)
		}
	}
	if IsValidStatus("closed") {
		t.Fatal("closed must not be a valid status")
	}
}

func TestNormalizeTrimsText(t *testing.T) {
	request := CreateIssueRequest{
		Title:       "  温度异常  ",
		Description: "  右侧偏高  ",
		Priority:    " high ",
	}
	request.Normalize()

	if request.Title != "温度异常" || request.Description != "右侧偏高" || request.Priority != PriorityHigh {
		t.Fatalf("unexpected normalized request: %+v", request)
	}
}
