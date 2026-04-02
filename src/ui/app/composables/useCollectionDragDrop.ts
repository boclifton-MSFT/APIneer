export interface DragPayload {
  type: 'request'
  requestId: string
  sourceCollectionId: string
  sourceFolderId: string | null
}

// Module-level shared state so all tree components see the same drag item
const currentDrag = ref<DragPayload | null>(null)

export function useCollectionDragDrop() {
  function startDrag(event: DragEvent, payload: DragPayload) {
    currentDrag.value = payload
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move'
      event.dataTransfer.setData('text/plain', payload.requestId)
    }
  }

  function endDrag() {
    currentDrag.value = null
  }

  return { currentDrag, startDrag, endDrag }
}
