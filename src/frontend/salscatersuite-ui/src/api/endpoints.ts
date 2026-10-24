/**
 * API surface used by the client. Paths are relative to the axios `baseURL`
 * (`/api`) so the browser always talks to its own origin and the Vite dev
 * proxy forwards the call to the API (no CORS pre-flight, no HTTPS redirect).
 */

export const API_ENDPOINTS = {
  auth: {
    login: '/Auth/login',
  },
  dashboard: {
    root: '/dashboard',
  },
  customers: {
    root: '/customers',
    /** The paged, filtered customer directory (spec §5.1). */
    directory: '/customers/directory',
    /** The XLSX export of the current filtered result set (spec §5.5). */
    directoryExport: '/customers/directory/export',
    /** A single customer profile (spec §5.2). */
    detail: (customerId: number) => `/customers/${customerId}`,
    /** The statement of account (spec §5.3). */
    statement: (customerId: number) => `/customers/${customerId}/statement`,
    /** The statement of account as a PDF (spec §5.4). */
    soa: (customerId: number) => `/customers/${customerId}/soa`,
  },
  menus: {
    /** The legacy unpaged menu list. */
    root: '/menus',
    /** The seeded course positions (spec §5.2). */
    courses: '/menus/courses',
    /** The paged, filtered dish catalogue with its costing summary (spec §6.1). */
    master: '/menus/master',
    /** The XLSX export of the current filtered result set (spec §6.3). */
    masterExport: '/menus/master/export',
    /** A single dish with its server-computed costing. */
    detail: (menuId: string) => `/menus/${menuId}`,
    /** The server-computed margin preview (spec §6.2). */
    costingPreview: '/menus/costing/preview',
    /** Creates a dish — `201 { menuId }`. */
    create: '/menus',
    /** Updates a dish — `204`. */
    update: (menuId: string) => `/menus/${menuId}`,
  },
  bookings: {
    /** The paged, filtered Bookings & Events Register (contract §14.1). */
    root: '/bookings',
    /** The register aggregate — the only source of the KPI figures (contract §14.2). */
    summary: '/bookings/summary',
    /** The server-computed quote for the workbench — it persists nothing (contract §14.3). */
    quote: '/bookings/quote',
    /** Creates a draft or a confirmed booking (contract §14.4). */
    create: '/bookings',
    /** The printable catering contract of one booking (contract §14.5). */
    contract: (transactionId: number) => `/bookings/${transactionId}/contract`,
    /** The XLSX export of the whole filtered register (contract §14.6). */
    export: '/bookings/export',
    /** The booking's dish selection, the tier's pools and the lock state (contract §15.1). */
    menus: (transactionId: number) => `/bookings/${transactionId}/menus`,
    /** The Kitchen Banquet Event Order PDF (contract §15.5). */
    beo: (transactionId: number) => `/bookings/${transactionId}/beo`,
    /** The booking's frozen add-on lines and its lock state (contract §16.3). */
    addons: (transactionId: number) => `/bookings/${transactionId}/addons`,
  },
  addons: {
    /** The add-on master — every active add-on (contract §16.1). */
    root: '/addons',
    /** The add-on category master (contract §16.2). */
    categories: '/addons/categories',
    /** The administration list — active and inactive, excluding soft-deleted (2026-10-24). */
    manage: '/addons/manage',
    /** One add-on for the administration form. */
    manageDetail: (addonId: number) => `/addons/manage/${addonId}`,
    /** Creates an add-on — `201 { addonId }`. */
    create: '/addons',
    /** Updates an add-on — `204`. */
    update: (addonId: number) => `/addons/${addonId}`,
    /** Soft-deletes an add-on — `204`. */
    remove: (addonId: number) => `/addons/${addonId}`,
  },
  departments: {
    /** The read-only department lookup (2026-10-24). */
    root: '/departments',
  },
  packages: {
    /** The whole, unpaged buffet tier catalogue. */
    root: '/packages',
    /** The server-computed catalogue aggregate — the only source of the KPI figures. */
    summary: '/packages/summary',
    /** A single buffet tier. */
    detail: (packageId: number) => `/packages/${packageId}`,
    /** Creates a tier — `201 { packageId }`. */
    create: '/packages',
    /** Updates a tier — `204`. */
    update: (packageId: number) => `/packages/${packageId}`,
    /** Activates or deactivates a tier — `200 { isActive }`. */
    toggleActive: (packageId: number) => `/packages/${packageId}/toggle-active`,
    /** The tier's course-coverage ruleset and its qualified pools (REQ-PKG-02). */
    courseRules: (packageId: number) => `/packages/${packageId}/course-rules`,
    /** The selection simulator and the headcount forecast (REQ-PKG-02). */
    courseRulePreview: (packageId: number) => `/packages/${packageId}/course-rules/preview`,
    /** The tier's coverage matrix (REQ-PKG-02). */
    areas: (packageId: number) => `/packages/${packageId}/areas`,
    /** Attaches a municipality to the tier — `201 { packageAreaCoverageId }`. */
    attachArea: (packageId: number) => `/packages/${packageId}/areas`,
    /** The out-of-town pricing simulator (REQ-PKG-02). */
    areaPreview: (packageId: number) => `/packages/${packageId}/areas/preview`,
    /** The tier's printable specification sheet (PDF). */
    specSheet: (packageId: number) => `/packages/${packageId}/spec-sheet`,
  },
  areas: {
    /** The municipality catalogue (REQ-PKG-02). */
    root: '/areas',
    /** Updates a municipality — `204`. */
    detail: (areaId: number) => `/areas/${areaId}`,
  },
} as const
