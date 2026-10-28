import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// vite.config.ts 未开启 test.globals，Testing Library 的自动清理不会生效。
// 这里显式 cleanup，避免上一个用例的 DOM 残留影响下一个用例的断言。
afterEach(cleanup)
