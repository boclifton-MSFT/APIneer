import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import McpPage from '~/pages/mcp.vue'

// ---------------------------------------------------------------------------
// Hoisted mock references — available inside vi.mock factory
// ---------------------------------------------------------------------------
const {
  mockGetServerConfigs,
  mockCreateServerConfig,
  mockMcpConnect,
  mockMcpDisconnect
} = vi.hoisted(() => ({
  mockGetServerConfigs: vi.fn().mockResolvedValue([]),
  mockCreateServerConfig: vi.fn().mockResolvedValue({
    id: 'new-id',
    name: 'New Server',
    transportType: 'stdio'
  }),
  mockMcpConnect: vi.fn().mockResolvedValue({
    connectionId: 'conn-1',
    capabilities: {},
    serverInfo: {}
  }),
  mockMcpDisconnect: vi.fn().mockResolvedValue(undefined)
}))

vi.mock('~/composables/useApi', () => ({
  useApi: () => ({
    getServerConfigs: mockGetServerConfigs,
    createServerConfig: mockCreateServerConfig,
    updateServerConfig: vi.fn().mockResolvedValue({}),
    deleteServerConfig: vi.fn().mockResolvedValue(undefined),
    mcpConnect: mockMcpConnect,
    mcpDisconnect: mockMcpDisconnect,
    mcpStatus: vi.fn().mockResolvedValue({}),
    mcpListTools: vi.fn().mockResolvedValue({ tools: [] }),
    mcpCallTool: vi.fn(),
    mcpListResources: vi.fn().mockResolvedValue({ resources: [] }),
    mcpReadResource: vi.fn(),
    mcpListPrompts: vi.fn().mockResolvedValue({ prompts: [] }),
    mcpGetPrompt: vi.fn(),
    mcpPing: vi.fn(),
    getRequests: vi.fn().mockResolvedValue([]),
    getRequest: vi.fn(),
    createRequest: vi.fn(),
    updateRequest: vi.fn(),
    deleteRequest: vi.fn(),
    sendRequest: vi.fn(),
    getCollections: vi.fn().mockResolvedValue([]),
    createCollection: vi.fn(),
    updateCollection: vi.fn(),
    deleteCollection: vi.fn(),
    getHistory: vi.fn().mockResolvedValue([]),
    clearHistory: vi.fn(),
    getEnvironments: vi.fn().mockResolvedValue([]),
    getEnvironment: vi.fn(),
    createEnvironment: vi.fn(),
    updateEnvironment: vi.fn(),
    deleteEnvironment: vi.fn(),
    addVariable: vi.fn(),
    updateVariable: vi.fn(),
    deleteVariable: vi.fn(),
    moveRequest: vi.fn(),
    reorderCollection: vi.fn()
  })
}))

// ---------------------------------------------------------------------------
// Sample data
// ---------------------------------------------------------------------------
const sampleServers = [
  {
    id: 'srv-1',
    name: 'SQLite Server',
    transportType: 'stdio' as const,
    command: 'npx server-sqlite',
    args: 'db.sqlite'
  },
  {
    id: 'srv-2',
    name: 'HTTP Server',
    transportType: 'streamable-http' as const,
    url: 'http://localhost:3000/mcp'
  }
]

// ---------------------------------------------------------------------------
// Mount helpers
// ---------------------------------------------------------------------------
async function mountPage() {
  const wrapper = await mountSuspended(McpPage)
  await flushPromises()
  return wrapper
}

async function mountWithServers() {
  mockGetServerConfigs.mockResolvedValue(sampleServers)
  const wrapper = await mountPage()
  return wrapper
}

async function mountWithServerSelected() {
  const wrapper = await mountWithServers()
  const serverItems = wrapper.findAll('.server-item')
  if (serverItems.length > 0) {
    await serverItems[0].trigger('click')
    await nextTick()
  }
  return wrapper
}

// ---------------------------------------------------------------------------
// Suite
// ---------------------------------------------------------------------------
beforeEach(() => {
  vi.clearAllMocks()
  mockGetServerConfigs.mockResolvedValue([])
  mockCreateServerConfig.mockResolvedValue({
    id: 'new-id',
    name: 'New Server',
    transportType: 'stdio'
  })
  mockMcpConnect.mockResolvedValue({
    connectionId: 'conn-1',
    capabilities: {},
    serverInfo: {}
  })
  mockMcpDisconnect.mockResolvedValue(undefined)
})

// ===========================================================================
// Server List Tests
// ===========================================================================
describe('Server List', () => {
  it('7 — shows empty state when no servers configured', async () => {
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('No MCP servers configured')
  })

  it('8 — shows "New Server" button in empty state', async () => {
    const wrapper = await mountPage()
    // The empty state renders a UButton with label "New Server"
    expect(wrapper.text()).toContain('New Server')
  })

  it('9 — renders saved servers with name and transport type', async () => {
    const wrapper = await mountWithServers()
    expect(wrapper.text()).toContain('SQLite Server')
    expect(wrapper.text()).toContain('HTTP Server')
    // Transport type badges
    expect(wrapper.text()).toContain('stdio')
    expect(wrapper.text()).toContain('HTTP')
  })
})

// ===========================================================================
// Connection Form Tests  (require a server to be selected)
// ===========================================================================
describe('Connection Form', () => {
  it('1 — renders transport type selector (stdio / streamable-http)', async () => {
    const wrapper = await mountWithServerSelected()
    const radios = wrapper.findAll('input[type="radio"][name="transportType"]')
    expect(radios.length).toBe(2)
    const values = radios.map(r => r.element.value)
    expect(values).toContain('stdio')
    expect(values).toContain('streamable-http')
  })

  it('2 — shows command, args, and env inputs when stdio is selected', async () => {
    const wrapper = await mountWithServerSelected()
    // srv-1 is stdio, so command/args/env fields should be visible
    const commandInput = wrapper.find('input[placeholder="npx @modelcontextprotocol/server-sqlite"]')
    const argsInput = wrapper.find('input[placeholder="db.sqlite"]')
    expect(commandInput.exists()).toBe(true)
    expect(argsInput.exists()).toBe(true)
    // Environment variables table
    expect(wrapper.find('.env-vars-section').exists()).toBe(true)
  })

  it('3 — shows URL input when streamable-http is selected', async () => {
    const wrapper = await mountWithServers()
    // Click the HTTP server (srv-2)
    const serverItems = wrapper.findAll('.server-item')
    await serverItems[1].trigger('click')
    await nextTick()

    const urlInput = wrapper.find('input[placeholder="http://localhost:3000/mcp"]')
    expect(urlInput.exists()).toBe(true)
    // Command/args should be hidden
    const commandInput = wrapper.find('input[placeholder="npx @modelcontextprotocol/server-sqlite"]')
    expect(commandInput.exists()).toBe(false)
  })

  it('4 — shows Connect button when disconnected', async () => {
    const wrapper = await mountWithServerSelected()
    const connectBtn = wrapper.find('.connect-button')
    expect(connectBtn.exists()).toBe(true)
    expect(connectBtn.text()).toContain('Connect')
  })

  it('5 — shows Disconnect button when connected', async () => {
    const wrapper = await mountWithServerSelected()
    // Click the connect button to trigger connection
    const connectBtn = wrapper.find('.connect-button')
    await connectBtn.trigger('click')
    await flushPromises()
    await nextTick()

    const disconnectBtn = wrapper.find('.disconnect-button')
    expect(disconnectBtn.exists()).toBe(true)
    expect(disconnectBtn.text()).toContain('Disconnect')
  })

  it('6 — server name input is present', async () => {
    const wrapper = await mountWithServerSelected()
    const nameInput = wrapper.find('input[placeholder="My MCP Server"]')
    expect(nameInput.exists()).toBe(true)
    // Should show the selected server's name
    expect((nameInput.element as HTMLInputElement).value).toBe('SQLite Server')
  })
})

// ===========================================================================
// Tab Tests  (require a server to be selected)
// ===========================================================================
describe('Capability Tabs', () => {
  it('10 — shows Tools, Resources, and Prompts tabs', async () => {
    const wrapper = await mountWithServerSelected()
    const tabButtons = wrapper.findAll('.tab-button')
    const labels = tabButtons.map(t => t.text())
    expect(labels).toContain('Tools')
    expect(labels).toContain('Resources')
    expect(labels).toContain('Prompts')
  })

  it('11 — tabs show placeholder message when not connected', async () => {
    const wrapper = await mountWithServerSelected()
    // Still in 'disconnected' state — tab-content should show a placeholder
    const tabContent = wrapper.find('.tab-content')
    expect(tabContent.text()).toContain('Connect to')
  })

  it('12 — default active tab is Tools', async () => {
    const wrapper = await mountWithServerSelected()
    const toolsTab = wrapper.findAll('.tab-button').find(t => t.text() === 'Tools')
    expect(toolsTab!.classes()).toContain('active')
  })
})

// ===========================================================================
// Integration Tests  (mock API calls)
// ===========================================================================
describe('Integration — API calls', () => {
  it('13 — creating a new server config calls createServerConfig', async () => {
    // Mount with servers so we can select one, which makes getServerConfigs get called again
    // For the create flow, we exercise it via the modal
    mockGetServerConfigs
      .mockResolvedValueOnce([]) // initial load (empty)
      .mockResolvedValue(sampleServers) // after create reload

    const wrapper = await mountPage()

    // Find and click the "New Server" button in empty state
    const newServerBtn = wrapper
      .findAll('button')
      .find(b => b.text().includes('New Server'))
    expect(newServerBtn).toBeDefined()
    await newServerBtn!.trigger('click')
    await nextTick()

    // The modal should now be open (or we can find the modal form in the document)
    // Look for the modal's name input in the whole document body
    const modalInputs = document.body.querySelectorAll('input[placeholder="My MCP Server"]')
    expect(modalInputs.length).toBeGreaterThan(0)

    // Fill in the name
    const nameInput = modalInputs[0] as HTMLInputElement
    nameInput.value = 'My New Server'
    nameInput.dispatchEvent(new Event('input', { bubbles: true }))
    await nextTick()

    // Find and click the Create button in the modal footer
    const createBtn = Array.from(document.body.querySelectorAll('button')).find(
      b => b.textContent?.includes('Create')
    )
    expect(createBtn).toBeDefined()
    await createBtn!.click()
    await flushPromises()

    expect(mockCreateServerConfig).toHaveBeenCalled()
  })

  it('14 — connecting to a server calls mcpConnect', async () => {
    const wrapper = await mountWithServerSelected()

    const connectBtn = wrapper.find('.connect-button')
    expect(connectBtn.exists()).toBe(true)
    await connectBtn.trigger('click')
    await flushPromises()

    expect(mockMcpConnect).toHaveBeenCalledWith(
      expect.objectContaining({
        serverId: 'srv-1',
        transportType: 'stdio'
      })
    )
  })

  it('15 — disconnecting calls mcpDisconnect', async () => {
    const wrapper = await mountWithServerSelected()

    // Connect first
    await wrapper.find('.connect-button').trigger('click')
    await flushPromises()
    await nextTick()

    // Now disconnect
    const disconnectBtn = wrapper.find('.disconnect-button')
    expect(disconnectBtn.exists()).toBe(true)
    await disconnectBtn.trigger('click')
    await flushPromises()

    expect(mockMcpDisconnect).toHaveBeenCalledWith('conn-1')
  })
})
