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

  it('reports a malformed payload instead of showing an empty queue', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ data: [] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    )

    render(<App />)

    expect(await screen.findByRole('alert')).toHaveTextContent('缺少 issues 数组')
  })

  it('loads an issue and updates its status', async () => {
    const issue = {
      id: 'issue-123',
      title: '1 号机柜温度异常',
      description: '机柜右侧温度偏高',
      priority: 'high',
      status: 'open',
      area: 'A 区 · 设备巡检',
      asset: '1 号机柜',
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

  it('requires an explicit confirmation before deleting an issue', async () => {
    const issue = {
      id: 'issue-456',
      title: '2 号机柜风扇异响',
      description: '风扇转速异常',
      priority: 'medium',
      status: 'in_progress',
      area: 'A 区 · 设备巡检',
      asset: '2 号机柜',
      position: { x: 0.5, y: 1.2, z: 4.4 },
      createdAt: '2026-10-27T10:00:00Z',
      updatedAt: '2026-10-27T10:05:00Z',
    }
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ issues: [issue] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    fireEvent.click(
      await screen.findByRole('button', { name: '删除“2 号机柜风扇异响”' }),
    )
    // 第一次点击只是展开确认框，不能直接删数据。
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(screen.getByText('移入回收站后可恢复，确认删除？')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: '确认删除' }))

    expect(
      await screen.findByText('“2 号机柜风扇异响”已移入回收站，可在回收站恢复'),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/issues/issue-456',
      expect.objectContaining({ method: 'DELETE' }),
    )
    expect(screen.queryByText('2 号机柜风扇异响')).not.toBeInTheDocument()
  })

  it('restores an archived issue from the recycle bin', async () => {
    const archivedIssue = {
      id: 'issue-789',
      title: '3 号机柜指示灯熄灭',
      description: '指示灯完全不亮',
      priority: 'high',
      status: 'resolved',
      area: 'A 区 · 设备巡检',
      asset: '3 号机柜',
      position: { x: 0.23, y: 2.3, z: 3.93 },
      createdAt: '2026-10-27T11:00:00Z',
      updatedAt: '2026-10-27T11:30:00Z',
      deletedAt: '2026-10-27T11:35:00Z',
    }
    const json = (value: unknown) =>
      new Response(JSON.stringify(value), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    const fetchMock = vi.fn().mockImplementation((url: string, init?: RequestInit) => {
      if (url === '/api/issues?deleted=1') {
        return Promise.resolve(json({ issues: [archivedIssue] }))
      }
      if (url.endsWith('/restore')) {
        return Promise.resolve(json({ ...archivedIssue, deletedAt: '' }))
      }
      return Promise.resolve(json({ issues: [] }))
    })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    fireEvent.click(await screen.findByRole('tab', { name: '回收站' }))

    expect(await screen.findByText('3 号机柜指示灯熄灭')).toBeInTheDocument()
    expect(screen.getByText(/归档于/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: '恢复“3 号机柜指示灯熄灭”' }))

    expect(
      await screen.findByText('“3 号机柜指示灯熄灭”已恢复到巡检队列'),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/issues/issue-789/restore',
      expect.objectContaining({ method: 'POST' }),
    )

    // 恢复后回到巡检队列，回收站里不再有这条记录。
    fireEvent.click(screen.getByRole('tab', { name: '巡检队列' }))
    expect(await screen.findByText('3 号机柜指示灯熄灭')).toBeInTheDocument()
  })

  it('exports the filtered queue as a CSV file', async () => {
    const issue = {
      id: 'issue-321',
      title: '1 号机柜温度异常',
      description: '右侧温度偏高，含逗号, 需要转义',
      priority: 'high',
      status: 'open',
      area: 'A 区 · 设备巡检',
      asset: '1 号机柜',
      position: { x: 1.25, y: 0.8, z: 2.4 },
      createdAt: '2026-10-27T09:00:00Z',
      updatedAt: '2026-10-27T09:00:00Z',
    }
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify({ issues: [issue] }), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          }),
        ),
    )

    const createObjectURL = vi.fn((_: Blob) => 'blob:mock')
    Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() })
    const clickSpy = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => {})

    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: '导出 CSV' }))

    expect(createObjectURL).toHaveBeenCalledTimes(1)
    expect(clickSpy).toHaveBeenCalledTimes(1)
    const blob = createObjectURL.mock.calls[0][0]
    // Blob.text() 会吃掉 BOM，所以直接从字节里确认 Excel 需要的 BOM 还在。
    const bytes = new Uint8Array(await blob.arrayBuffer())
    expect([bytes[0], bytes[1], bytes[2]]).toEqual([0xef, 0xbb, 0xbf])
    const csv = await blob.text()
    expect(csv).toContain('问题ID,标题,描述,优先级,状态,区域,设备')
    // 描述里的逗号必须被引号包住，否则 Excel 会错列。
    expect(csv).toContain('"右侧温度偏高，含逗号, 需要转义"')

    clickSpy.mockRestore()
  })
})
