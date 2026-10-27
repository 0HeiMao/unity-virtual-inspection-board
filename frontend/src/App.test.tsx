import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows a recoverable error when the backend is unavailable', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('network down')))

    render(<App />)

    expect(await screen.findByRole('alert')).toHaveTextContent('无法连接后端')
    expect(screen.getByRole('button', { name: '重试连接' })).toBeEnabled()
    expect(screen.getByText('问题队列')).toBeInTheDocument()
  })

  it('loads an issue and updates its status', async () => {
    const issue = {
      id: 'issue-123',
      title: '1 号机柜温度异常',
      description: '机柜右侧温度偏高',
      priority: 'high',
      status: 'open',
      position: { x: 1.25, y: 0.8, z: 2.4 },
      createdAt: '2026-10-27T09:00:00Z',
      updatedAt: '2026-10-27T09:00:00Z',
    }
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ issues: [issue] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ ...issue, status: 'resolved' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      )
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    const statusSelect = await screen.findByRole('combobox', {
      name: '修改“1 号机柜温度异常”的处理状态',
    })
    expect(statusSelect).toHaveValue('open')

    fireEvent.change(statusSelect, { target: { value: 'resolved' } })

    expect(await screen.findByText('“1 号机柜温度异常”已更新为已解决')).toBeInTheDocument()
    expect(statusSelect).toHaveValue('resolved')
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/issues/issue-123/status',
      expect.objectContaining({
        method: 'PATCH',
        body: JSON.stringify({ status: 'resolved' }),
      }),
    )
  })
})
