export interface RpcHistoryEntry {
  id: number
  timestamp: Date
  method: string
  status: 'success' | 'error'
  request: unknown
  response: unknown
}

const MAX_HISTORY = 50

// Module-level singleton so all MCP components share one history list
const rpcHistory = ref<RpcHistoryEntry[]>([])
let rpcIdCounter = 0

export function useMcpRpcHistory() {
  function addRpcEntry(method: string, request: unknown, response: unknown, isError: boolean) {
    rpcIdCounter++
    rpcHistory.value.unshift({
      id: rpcIdCounter,
      timestamp: new Date(),
      method,
      status: isError ? 'error' : 'success',
      request,
      response
    })
    if (rpcHistory.value.length > MAX_HISTORY) {
      rpcHistory.value = rpcHistory.value.slice(0, MAX_HISTORY)
    }
  }

  function clearRpcHistory() {
    rpcHistory.value = []
    rpcIdCounter = 0
  }

  return {
    rpcHistory: readonly(rpcHistory),
    addRpcEntry,
    clearRpcHistory
  }
}
