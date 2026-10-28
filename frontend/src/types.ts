export type Priority = 'low' | 'medium' | 'high'
export type IssueStatus = 'open' | 'in_progress' | 'resolved'

export interface Position {
  x: number
  y: number
  z: number
}

export interface Issue {
  id: string
  title: string
  description: string
  priority: Priority
  status: IssueStatus
  /** 区域归属，例如「A 区 · 设备巡检」；老数据可能为空字符串。 */
  area: string
  /** 设备归属，例如「2 号机柜」；老数据可能为空字符串。 */
  asset: string
  position: Position
  createdAt: string
  updatedAt: string
  /** 归档时间；为空或缺失表示问题仍在巡检队列里。 */
  deletedAt?: string
}
