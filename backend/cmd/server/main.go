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

	"inspection-board/backend/internal/httpapi"
	"inspection-board/backend/internal/store"
)

func main() {
	dataFile := envOrDefault("DATA_FILE", "data/issues.json")
	address := envOrDefault("ADDR", ":8080")
	logger := log.New(os.Stdout, "[inspection-api] ", log.LstdFlags)

	issues, err := store.OpenJSON(dataFile)
	if err != nil {
		logger.Fatalf("open data store: %v", err)
	}

	server := &http.Server{
		Addr:              address,
		Handler:           httpapi.New(issues, logger),
		ReadHeaderTimeout: 5 * time.Second,
		ReadTimeout:       10 * time.Second,
		WriteTimeout:      10 * time.Second,
		IdleTimeout:       60 * time.Second,
	}

	go func() {
		logger.Printf("listening on http://localhost%s (data: %s)", address, dataFile)
		if err := server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			logger.Fatalf("serve: %v", err)
		}
	}()

	stop, cancel := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer cancel()
	<-stop.Done()

	shutdownContext, shutdownCancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer shutdownCancel()
	if err := server.Shutdown(shutdownContext); err != nil {
		logger.Printf("shutdown: %v", err)
	}
}

func envOrDefault(name, fallback string) string {
	if value := os.Getenv(name); value != "" {
		return value
	}
	return fallback
}
