import type { Issue, IssueStatus } from './types'

const configuredBase = import.meta.env.VITE_API_BASE_URL?.trim()
const API_BASE = (configuredBase || '/api').replace(/\/$/, '')

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), 8000)

  try {
    const response = await fetch(`${API_BASE}${path}`, {
      ...init,
      signal: controller.signal,
      headers: {
        Accept: 'application/json',
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        ...init?.headers,
      },
    })

    const body = (await response.json().catch(() => null)) as
      | { error?: string; issues?: Issue[] }
      | Issue
      | null

    if (!response.ok) {
      const message =
        body && typeof body === 'object' && 'error' in body && body.error
          ? body.error
          : `后端返回 ${response.status}`
      throw new ApiError(message, response.status)
    }
    return body as T
  } catch (error) {
    if (error instanceof ApiError) {
      throw error
    }
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new ApiError('请求超时，请确认 Go 服务是否正常运行')
    }
    throw new ApiError('无法连接后端，请确认 Go 服务已启动在 8080 端口')
  } finally {
    window.clearTimeout(timeout)
  }
}

export async function fetchIssues(): Promise<Issue[]> {
  const payload = await request<{ issues: Issue[] }>('/issues')
  return Array.isArray(payload.issues) ? payload.issues : []
}

export function updateIssueStatus(id: string, status: IssueStatus): Promise<Issue> {
  return request<Issue>(`/issues/${encodeURIComponent(id)}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  })
}
