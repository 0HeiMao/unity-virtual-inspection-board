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

export type IssueScope = 'active' | 'deleted'

export async function fetchIssues(scope: IssueScope = 'active'): Promise<Issue[]> {
  const path = scope === 'deleted' ? '/issues?deleted=1' : '/issues'
  const payload = await request<{ issues?: unknown } | null>(path)
  if (!payload || typeof payload !== 'object' || !Array.isArray(payload.issues)) {
    // 宁可报错也不要把协议异常伪装成"暂无问题"，否则管理端会静默显示空列表。
    throw new ApiError('后端返回格式异常：响应中缺少 issues 数组')
  }
  return (payload.issues as Issue[]).map(normalizeIssue)
}

/** 补齐老数据缺失的归属字段，避免界面出现 undefined。 */
function normalizeIssue(raw: Issue): Issue {
  return {
    ...raw,
    area: typeof raw.area === 'string' ? raw.area : '',
    asset: typeof raw.asset === 'string' ? raw.asset : '',
  }
}

export function updateIssueStatus(id: string, status: IssueStatus): Promise<Issue> {
  return request<Issue>(`/issues/${encodeURIComponent(id)}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  })
}

export async function deleteIssue(id: string): Promise<void> {
  // 后端是软删除：问题被归档到回收站，仍可用 restoreIssue 恢复。
  await request<null>(`/issues/${encodeURIComponent(id)}`, { method: 'DELETE' })
}

export function restoreIssue(id: string): Promise<Issue> {
  return request<Issue>(`/issues/${encodeURIComponent(id)}/restore`, { method: 'POST' })
}
