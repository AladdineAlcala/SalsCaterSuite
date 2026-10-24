import { useCallback, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  CircleCheck,
  CircleSlash,
  PackagePlus,
  Pencil,
  Plus,
  RefreshCw,
  Trash2,
  X,
} from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { EmptyState } from '@/components/feedback/EmptyState'
import { SpinnerLoader } from '@/components/feedback/SpinnerLoader'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { CurrencyInput } from '@/components/ui/CurrencyInput'
import { formatCurrency } from '@/utils/currencyFormatter'
import { parseCurrencyInput } from '@/utils/currencyInput'
import type { AddonAdminItem, AddonPricingBasis, AddonUpsertRequest } from '@/types/addon.types'
import {
  useAddonCategoryLookup,
  useAddonManagementList,
  useCreateAddon,
  useDeleteAddon,
  useDepartmentLookup,
  useUpdateAddon,
} from './hooks/useAddonManagement'

interface AddonFormValues {
  description: string
  addonCategoryId: string
  deptId: string
  pricingBasis: '' | AddonPricingBasis
  unitPrice: string
  unit: string
  isActive: boolean
}

type AddonFormErrors = Partial<
  Record<'description' | 'unitPrice' | 'unit', string>
>

const EMPTY_FORM: AddonFormValues = {
  description: '',
  addonCategoryId: '',
  deptId: '',
  pricingBasis: '',
  unitPrice: '',
  unit: '',
  isActive: true,
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-md text-body-md w-full'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

function formValuesFromAddon(addon: AddonAdminItem): AddonFormValues {
  return {
    description: addon.description,
    addonCategoryId:
      addon.addonCategoryId === null ? '' : String(addon.addonCategoryId),
    deptId: addon.deptId === null ? '' : String(addon.deptId),
    pricingBasis: addon.pricingBasis ?? '',
    unitPrice: String(addon.unitPrice),
    unit: addon.unit ?? '',
    isActive: addon.isActive,
  }
}

function validateForm(values: AddonFormValues): AddonFormErrors {
  const errors: AddonFormErrors = {}
  const description = values.description.trim()

  if (description.length === 0) {
    errors.description = 'Add-on description is required.'
  } else if (description.length > 150) {
    errors.description = 'Add-on description must not exceed 150 characters.'
  }

  if (values.unit.trim().length > 50) {
    errors.unit = 'Unit must not exceed 50 characters.'
  }

  if (values.unitPrice.trim().length === 0) {
    errors.unitPrice = 'Unit price is required.'
  } else {
    const price = parseCurrencyInput(values.unitPrice)
    if (price === null) {
      errors.unitPrice = 'Unit price must be a number.'
    } else if (price < 0) {
      errors.unitPrice = 'Unit price must be zero or greater.'
    }
  }

  return errors
}

function toRequest(values: AddonFormValues): AddonUpsertRequest | null {
  const price = parseCurrencyInput(values.unitPrice)
  if (price === null) return null

  return {
    addonCategoryId:
      values.addonCategoryId.trim().length === 0
        ? null
        : Number(values.addonCategoryId),
    deptId: values.deptId.trim().length === 0 ? null : Number(values.deptId),
    pricingBasis: values.pricingBasis === '' ? null : values.pricingBasis,
    description: values.description.trim(),
    unitPrice: price,
    unit: values.unit.trim().length === 0 ? null : values.unit.trim(),
    isActive: values.isActive,
  }
}

function basisLabel(basis: AddonPricingBasis | null): string {
  if (basis === 'Flat') return 'Flat'
  if (basis === 'PerGuest') return 'Per guest'
  return 'Not stated (Flat)'
}

/**
 * The Add-on Master administration screen (`/settings/addons`).
 *
 * It is the write surface the booking pickers lacked: the master is listed with
 * its **inactive** rows so an administrator can reactivate one, and the create /
 * edit form maps 1:1 onto `AddonDetail`. The list and every figure are the
 * server's; the client formats and validates, and never derives a price.
 */
export function AddonMasterPage() {
  const addonsQuery = useAddonManagementList()
  const categoriesQuery = useAddonCategoryLookup()
  const departmentsQuery = useDepartmentLookup()
  const createAddon = useCreateAddon()
  const updateAddon = useUpdateAddon()
  const deleteAddon = useDeleteAddon()

  const [search, setSearch] = useState('')
  const [isFormOpen, setIsFormOpen] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [values, setValues] = useState<AddonFormValues>(EMPTY_FORM)
  const [fieldErrors, setFieldErrors] = useState<AddonFormErrors>({})
  const [serverErrors, setServerErrors] = useState<string[]>([])
  const [notice, setNotice] = useState<string | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<AddonAdminItem | null>(null)

  const addons = addonsQuery.data ?? []

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (term.length === 0) return addons

    return addons.filter((addon) =>
      [addon.description, addon.addonCategoryName ?? '', addon.deptName ?? '']
        .some((value) => value.toLowerCase().includes(term)),
    )
  }, [addons, search])

  const openCreate = useCallback(() => {
    setEditingId(null)
    setValues(EMPTY_FORM)
    setFieldErrors({})
    setServerErrors([])
    setNotice(null)
    setIsFormOpen(true)
  }, [])

  const openEdit = useCallback((addon: AddonAdminItem) => {
    setEditingId(addon.addonId)
    setValues(formValuesFromAddon(addon))
    setFieldErrors({})
    setServerErrors([])
    setNotice(null)
    setIsFormOpen(true)
  }, [])

  const closeForm = useCallback(() => {
    setIsFormOpen(false)
    setEditingId(null)
    setFieldErrors({})
    setServerErrors([])
  }, [])

  const handleChange = useCallback(
    <K extends keyof AddonFormValues>(field: K, value: AddonFormValues[K]) => {
      setValues((current) => ({ ...current, [field]: value }))
      setFieldErrors((current) => {
        if (current[field as keyof AddonFormErrors] === undefined) return current
        const next = { ...current }
        delete next[field as keyof AddonFormErrors]
        return next
      })
    },
    [],
  )

  const handleSave = useCallback(() => {
    const problems = validateForm(values)
    if (Object.keys(problems).length > 0) {
      setFieldErrors(problems)
      setServerErrors([])
      return
    }

    const request = toRequest(values)
    if (request === null) {
      setFieldErrors({ unitPrice: 'Unit price must be a number.' })
      return
    }

    const onError = (error: unknown) => setServerErrors(readErrorMessages(error))

    if (editingId !== null) {
      updateAddon.mutate(
        { addonId: editingId, request },
        {
          onSuccess: () => {
            closeForm()
            setNotice('The add-on was updated.')
          },
          onError,
        },
      )
      return
    }

    createAddon.mutate(request, {
      onSuccess: () => {
        closeForm()
        setNotice('The add-on was created.')
      },
      onError,
    })
  }, [values, editingId, updateAddon, createAddon, closeForm])

  const handleDelete = useCallback(() => {
    if (deleteTarget === null) return
    deleteAddon.mutate(deleteTarget.addonId, {
      onSuccess: () => {
        setDeleteTarget(null)
        setNotice('The add-on was deleted.')
      },
      onError: (error) => setServerErrors(readErrorMessages(error)),
    })
  }, [deleteTarget, deleteAddon])

  const isSaving = createAddon.isPending || updateAddon.isPending
  const isDeleting = deleteAddon.isPending
  const isLoadingLookups = categoriesQuery.isPending || departmentsQuery.isPending

  return (
    <div className="flex flex-col gap-space-xl">
      <Card
        elevation={1}
        className="flex flex-col lg:flex-row lg:items-center justify-between gap-space-md"
      >
        <div className="flex flex-col gap-1 min-w-0">
          <nav
            aria-label="Breadcrumb"
            className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-400 flex-wrap"
          >
            <span>Catering Operations</span>
            <span aria-hidden="true">/</span>
            <Link to="/settings" className="hover:text-espresso-900 underline">
              Settings
            </Link>
            <span aria-hidden="true">/</span>
            <span className="font-semibold text-espresso-850">Add-ons</span>
          </nav>
          <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight flex items-center gap-2">
            <PackagePlus className="h-6 w-6 text-culinary-700" aria-hidden="true" />
            Add-on Master
          </h1>
          <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
            Create, edit, deactivate or delete the add-ons the booking screens offer. The list
            includes the inactive ones; a deleted add-on disappears from both this list and the
            booking picker.
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Button variant="primary" icon={Plus} onClick={openCreate}>
            New add-on
          </Button>
        </div>
      </Card>

      {notice ? (
        <AlertBanner
          variant="info"
          title="Add-on master updated"
          errors={[notice]}
          action={
            <Button variant="secondary" size="sm" onClick={() => setNotice(null)}>
              Dismiss
            </Button>
          }
        />
      ) : null}

      {serverErrors.length > 0 ? (
        <AlertBanner
          title="The add-on was rejected"
          errors={serverErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setServerErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {addonsQuery.isError ? (
        <AlertBanner
          title="The add-on master could not be loaded"
          errors={readErrorMessages(addonsQuery.error)}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => void addonsQuery.refetch()}
            >
              Retry
            </Button>
          }
        />
      ) : null}

      {deleteTarget ? (
        <AlertBanner
          variant="warning"
          title="Delete this add-on?"
          errors={[
            `"${deleteTarget.description}" will be removed from the master. A booking that already carries it keeps its frozen line.`,
          ]}
          action={
            <div className="flex items-center gap-2">
              <Button variant="secondary" size="sm" onClick={() => setDeleteTarget(null)}>
                Cancel
              </Button>
              <Button
                variant="primary"
                size="sm"
                icon={Trash2}
                onClick={handleDelete}
                disabled={isDeleting}
              >
                {isDeleting ? 'Deleting…' : 'Delete add-on'}
              </Button>
            </div>
          }
        />
      ) : null}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter items-start">
        <div className={isFormOpen ? 'lg:col-span-7 xl:col-span-8' : 'lg:col-span-12'}>
          <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
              <h2 className="font-headline-sm text-headline-sm text-espresso-900">
                Add-ons
              </h2>
              <label className="flex flex-col gap-1 min-w-0 sm:w-72">
                <span className="font-caption text-caption text-espresso-400 font-semibold uppercase">
                  Search
                </span>
                <input
                  type="search"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search description, category or department…"
                  className={INPUT_CLASSES}
                />
              </label>
            </div>

            {addonsQuery.isPending ? <SpinnerLoader label="Loading the add-on master" /> : null}

            {!addonsQuery.isPending && addons.length === 0 ? (
              <EmptyState
                icon={PackagePlus}
                title="No add-ons are defined yet"
                description="Create the first add-on, then it will be available to the booking screens."
              />
            ) : null}

            {addons.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <caption className="sr-only">
                    The add-on master, including inactive add-ons.
                  </caption>
                  <thead>
                    <tr className="border-b border-artisan-border">
                      {[
                        'Add-on',
                        'Category',
                        'Department',
                        'Basis',
                        'Unit price',
                        'State',
                        'Actions',
                      ].map((heading) => (
                        <th
                          key={heading}
                          scope="col"
                          className="py-2 pr-3 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold"
                        >
                          {heading}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map((addon) => (
                      <tr key={addon.addonId} className="border-b border-artisan-border align-top">
                        <th
                          scope="row"
                          className="py-2.5 pr-3 font-body-sm text-body-sm text-espresso-900 font-semibold break-words"
                        >
                          {addon.description}
                        </th>
                        <td className="py-2.5 pr-3 font-body-sm text-body-sm text-espresso-700">
                          {addon.addonCategoryName ?? 'Not stated'}
                        </td>
                        <td className="py-2.5 pr-3 font-body-sm text-body-sm text-espresso-700">
                          {addon.deptName ?? 'Not stated'}
                        </td>
                        <td className="py-2.5 pr-3 font-body-sm text-body-sm text-espresso-700">
                          {basisLabel(addon.pricingBasis)}
                        </td>
                        <td className="py-2.5 pr-3 font-data-sm text-data-sm text-espresso-900">
                          {formatCurrency(addon.unitPrice)}
                          {addon.unit === null ? null : (
                            <span className="font-caption text-caption text-espresso-400">
                              {' '}
                              / {addon.unit}
                            </span>
                          )}
                        </td>
                        <td className="py-2.5 pr-3">
                          <Badge
                            tone={addon.isActive ? 'success' : 'neutral'}
                            icon={addon.isActive ? CircleCheck : CircleSlash}
                          >
                            {addon.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                        </td>
                        <td className="py-2.5">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <Button
                              variant="secondary"
                              size="sm"
                              icon={Pencil}
                              onClick={() => openEdit(addon)}
                              aria-label={`Edit ${addon.description}`}
                            >
                              Edit
                            </Button>
                            <Button
                              variant="tertiary"
                              size="sm"
                              icon={Trash2}
                              onClick={() => {
                                setServerErrors([])
                                setDeleteTarget(addon)
                              }}
                              className="text-error"
                              aria-label={`Delete ${addon.description}`}
                            >
                              Delete
                            </Button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : null}

            {addons.length > 0 && filtered.length === 0 ? (
              <p className="font-body-sm text-body-sm text-espresso-600">
                No add-on matches the search.
              </p>
            ) : null}
          </Card>
        </div>

        {isFormOpen ? (
          <div className="lg:col-span-5 xl:col-span-4">
            <Card elevation={1} className="flex flex-col gap-space-md">
              <div className="flex items-center justify-between gap-2">
                <h2 className="font-headline-sm text-headline-sm text-espresso-900">
                  {editingId === null ? 'New add-on' : 'Edit add-on'}
                </h2>
                <Button
                  variant="tertiary"
                  size="sm"
                  icon={X}
                  onClick={closeForm}
                  aria-label="Close the add-on form"
                >
                  Close
                </Button>
              </div>

              {isLoadingLookups ? (
                <p className="font-body-sm text-body-sm text-espresso-600">
                  Loading the category and department lookups…
                </p>
              ) : null}

              <div className="flex flex-col gap-3">
                <div className="flex flex-col gap-1">
                  <label
                    htmlFor="addon-description"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Description
                  </label>
                  <input
                    id="addon-description"
                    type="text"
                    maxLength={150}
                    value={values.description}
                    onChange={(event) => handleChange('description', event.target.value)}
                    aria-invalid={fieldErrors.description ? true : undefined}
                    aria-describedby={
                      fieldErrors.description ? 'addon-description-error' : undefined
                    }
                    className={INPUT_CLASSES}
                  />
                  {fieldErrors.description ? (
                    <p
                      id="addon-description-error"
                      role="alert"
                      className="font-caption text-caption text-error"
                    >
                      {fieldErrors.description}
                    </p>
                  ) : null}
                </div>

                <div className="flex flex-col gap-1">
                  <label
                    htmlFor="addon-category"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Category
                  </label>
                  <select
                    id="addon-category"
                    value={values.addonCategoryId}
                    onChange={(event) => handleChange('addonCategoryId', event.target.value)}
                    className={INPUT_CLASSES}
                  >
                    <option value="">Not stated</option>
                    {(categoriesQuery.data ?? []).map((category) => (
                      <option
                        key={category.addonCategoryId}
                        value={String(category.addonCategoryId)}
                      >
                        {category.addonCategoryName}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="flex flex-col gap-1">
                  <label
                    htmlFor="addon-department"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Department
                  </label>
                  <select
                    id="addon-department"
                    value={values.deptId}
                    onChange={(event) => handleChange('deptId', event.target.value)}
                    className={INPUT_CLASSES}
                  >
                    <option value="">Not stated</option>
                    {(departmentsQuery.data ?? []).map((department) => (
                      <option key={department.deptId} value={String(department.deptId)}>
                        {department.deptName}
                      </option>
                    ))}
                  </select>
                  {(departmentsQuery.data ?? []).length === 0 ? (
                    <p className="font-caption text-caption text-espresso-600">
                      No departments are defined yet. Leave this empty.
                    </p>
                  ) : null}
                </div>

                <div className="flex flex-col gap-1">
                  <label
                    htmlFor="addon-pricing-basis"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Pricing basis
                  </label>
                  <select
                    id="addon-pricing-basis"
                    value={values.pricingBasis}
                    onChange={(event) =>
                      handleChange(
                        'pricingBasis',
                        event.target.value as '' | AddonPricingBasis,
                      )
                    }
                    className={INPUT_CLASSES}
                  >
                    <option value="">Not stated (treated as Flat)</option>
                    <option value="Flat">Flat — the whole line</option>
                    <option value="PerGuest">Per guest</option>
                  </select>
                </div>

                <div className="flex flex-col gap-1">
                  <label
                    htmlFor="addon-unit-price"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Unit price
                  </label>
                  <CurrencyInput
                    id="addon-unit-price"
                    value={values.unitPrice}
                    onValueChange={(value) => handleChange('unitPrice', value)}
                    aria-invalid={fieldErrors.unitPrice ? true : undefined}
                    aria-describedby={
                      fieldErrors.unitPrice ? 'addon-unit-price-error' : 'addon-unit-price-hint'
                    }
                    className={INPUT_CLASSES}
                  />
                  <p
                    id="addon-unit-price-hint"
                    className="font-caption text-caption text-espresso-600"
                  >
                    The unit amount — per guest when the basis is per guest, the whole line
                    when it is flat.
                  </p>
                  {fieldErrors.unitPrice ? (
                    <p
                      id="addon-unit-price-error"
                      role="alert"
                      className="font-caption text-caption text-error"
                    >
                      {fieldErrors.unitPrice}
                    </p>
                  ) : null}
                </div>

                <div className="flex flex-col gap-1">
                  <label
                    htmlFor="addon-unit"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Unit of measure
                  </label>
                  <input
                    id="addon-unit"
                    type="text"
                    maxLength={50}
                    value={values.unit}
                    onChange={(event) => handleChange('unit', event.target.value)}
                    aria-invalid={fieldErrors.unit ? true : undefined}
                    aria-describedby={fieldErrors.unit ? 'addon-unit-error' : undefined}
                    className={INPUT_CLASSES}
                    placeholder="e.g. event, guest, platter"
                  />
                  {fieldErrors.unit ? (
                    <p
                      id="addon-unit-error"
                      role="alert"
                      className="font-caption text-caption text-error"
                    >
                      {fieldErrors.unit}
                    </p>
                  ) : null}
                </div>

                <label className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-800">
                  <input
                    type="checkbox"
                    checked={values.isActive}
                    onChange={(event) => handleChange('isActive', event.target.checked)}
                    className="h-4 w-4 rounded border-artisan-border text-culinary-600 focus:ring-2 focus:ring-culinary-500/20"
                  />
                  Active — offered by the booking screens
                </label>
              </div>

              <div className="flex items-center justify-end gap-2 border-t border-artisan-border pt-space-sm">
                <Button variant="secondary" onClick={closeForm}>
                  Cancel
                </Button>
                <Button variant="primary" onClick={handleSave} disabled={isSaving}>
                  {isSaving ? 'Saving…' : editingId === null ? 'Create add-on' : 'Save changes'}
                </Button>
              </div>
            </Card>
          </div>
        ) : null}
      </div>
    </div>
  )
}
