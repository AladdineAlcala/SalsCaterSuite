import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { SpinnerLoader } from '@/components/feedback/SpinnerLoader'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { MenuCostBar } from './components/MenuCostBar'
import { MenuPricingSimulator } from './components/MenuPricingSimulator'
import {
  EMPTY_MENU_FORM_VALUES,
  MenuRecipeForm,
  menuFormValuesFromItem,
  parseAmount,
  toUpsertRequest,
  validateMenuForm,
} from './components/MenuRecipeForm'
import type { MenuFormValues } from './components/MenuRecipeForm'
import { MenuSummaryCards } from './components/MenuSummaryCards'
import { useMenuCourses } from './hooks/useMenuCourses'
import { useMenuDetail } from './hooks/useMenuMaster'
import { useCreateMenu, useUpdateMenu } from './hooks/useMenuMutations'
import { useMenuPreview } from './hooks/useMenuPreview'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/**
 * The Create / Edit Recipe screen (spec §5).
 *
 * The screen holds the dish master record and a **server-computed** margin
 * preview. Every figure it shows — the food-cost percentage, the gross
 * profit, the gross margin, the variance and the benchmark — comes from
 * `POST /api/menus/costing/preview` or from the dish the API published. The
 * browser formats and displays, it never computes (DEC-002).
 */
export function MenuRecipeFormPage() {
  const navigate = useNavigate()
  const params = useParams<{ menuId?: string }>()
  const menuId = params.menuId ?? null
  const isEdit = menuId !== null

  const [values, setValues] = useState<MenuFormValues>(EMPTY_MENU_FORM_VALUES)
  const [clientErrors, setClientErrors] = useState<string[]>([])
  const [serverErrors, setServerErrors] = useState<string[]>([])
  const [menuIdError, setMenuIdError] = useState<string | null>(null)

  const { data: dish, isPending: isLoadingDish, isError: isDishError, error: dishError, refetch } =
    useMenuDetail(menuId)

  const { data: courses, isPending: isLoadingCourses } = useMenuCourses()

  const createMenu = useCreateMenu()
  const updateMenu = useUpdateMenu()

  // The form is seeded once from the dish the API published; a rejected save
  // never resets what the operator typed (AC-M16).
  const seededMenuId = useRef<string | null>(null)
  useEffect(() => {
    if (dish && seededMenuId.current !== dish.menuId) {
      seededMenuId.current = dish.menuId
      setValues(menuFormValuesFromItem(dish))
    }
  }, [dish])

  const previewInput = useMemo(
    () => ({
      sellingPrice: parseAmount(values.price),
      foodCost: parseAmount(values.foodCost),
    }),
    [values.price, values.foodCost],
  )

  const { data: preview, isFetching: isPreviewing, error: previewError } =
    useMenuPreview(previewInput)

  const handleChange = useCallback(
    (field: keyof MenuFormValues, value: string | number | null) => {
      setValues((current) => ({ ...current, [field]: value }))
      if (field === 'menuId') {
        setMenuIdError(null)
      }
    },
    [],
  )

  const handleCancel = useCallback(() => {
    navigate('/menus')
  }, [navigate])

  const handleSave = useCallback(() => {
    const problems = validateMenuForm(values)
    if (problems.length > 0) {
      setClientErrors(problems)
      return
    }

    const request = toUpsertRequest(values)
    if (request === null) {
      setClientErrors(['The recipe is incomplete.'])
      return
    }

    setClientErrors([])
    setServerErrors([])
    setMenuIdError(null)

    const onError = (error: unknown) => {
      if (error instanceof ApiError && error.status === 409) {
        // A duplicate recipe id is a field-level answer (AC-M16).
        setMenuIdError(readErrorMessages(error)[0])
        return
      }
      setServerErrors(readErrorMessages(error))
    }

    if (isEdit && menuId !== null) {
      updateMenu.mutate(
        { menuId, request },
        {
          onSuccess: () => navigate('/menus'),
          onError,
        },
      )
      return
    }

    createMenu.mutate(request, {
      onSuccess: () => navigate('/menus'),
      onError,
    })
  }, [values, isEdit, menuId, updateMenu, createMenu, navigate])

  const isSaving = createMenu.isPending || updateMenu.isPending
  const previewErrorMessage =
    previewError && previewError instanceof ApiError
      ? readErrorMessages(previewError)[0]
      : null

  return (
    <div className="flex flex-col gap-space-xl">
      <Card elevation={1} className="flex flex-col lg:flex-row lg:items-center justify-between gap-space-md">
        <div className="flex flex-col gap-1 min-w-0">
          <nav
            aria-label="Breadcrumb"
            className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-400 flex-wrap"
          >
            <span>Kitchen Commissary</span>
            <span aria-hidden="true">/</span>
            <span>Menu &amp; Food Costing</span>
            <span aria-hidden="true">/</span>
            <span className="font-semibold text-espresso-850">
              {isEdit ? 'Edit Recipe' : 'New Recipe Formulation'}
            </span>
          </nav>
          <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
            {isEdit ? 'Edit Recipe' : 'Create Recipe'}
          </h1>
          <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
            The dish master record with a live, server-computed margin preview against the
            configured food-cost benchmark.
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Button variant="secondary" icon={ArrowLeft} onClick={handleCancel}>
            Discard / Cancel
          </Button>
        </div>
      </Card>

      {clientErrors.length > 0 ? (
        <AlertBanner
          variant="warning"
          title="The recipe is incomplete"
          errors={clientErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setClientErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {serverErrors.length > 0 ? (
        <AlertBanner
          title="The recipe was rejected"
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

      {isDishError ? (
        <AlertBanner
          title="The recipe could not be loaded"
          errors={readErrorMessages(dishError)}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={() => void refetch()}>
              Retry
            </Button>
          }
        />
      ) : null}

      {isEdit && isLoadingDish ? <SpinnerLoader label="Loading the recipe" /> : null}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter items-start">
        <div className="lg:col-span-5 xl:col-span-4 flex flex-col gap-space-md min-w-0">
          <MenuRecipeForm
            mode={isEdit ? 'edit' : 'create'}
            values={values}
            onChange={handleChange}
            courses={courses ?? []}
            isLoadingCourses={isLoadingCourses}
            menuIdError={menuIdError}
          />

          <MenuPricingSimulator
            price={values.price}
            onPriceChange={(value) => handleChange('price', value)}
            foodCost={values.foodCost}
            onFoodCostChange={(value) => handleChange('foodCost', value)}
            preview={preview}
            isPreviewing={isPreviewing}
            previewError={previewErrorMessage}
          />

          <CapabilityGapNotice
            title="Plating & prep directives unavailable"
            missing="A dish carries no plating standard, no kitchen station, no critical-control-point directive and no commissioning instruction."
            requirement="REQ-MNU-03"
          />
        </div>

        <div className="lg:col-span-7 xl:col-span-8 flex flex-col gap-space-md min-w-0">
          <MenuSummaryCards preview={preview} isPreviewing={isPreviewing} />

          <CapabilityGapNotice
            title="Yield & shrinkage factors unavailable"
            missing="Trimming loss, cooking shrinkage and the buffet buffer are not stored for a dish, so no yield gauge can be shown."
            requirement="REQ-MNU-03"
          />

          <CapabilityGapNotice
            title="Ingredient formulation table unavailable"
            missing="There is no ingredient master, no supplier price, no FIFO rate and no batch scale, so no formulation table can be produced."
            requirement="REQ-MNU-03"
          />
        </div>
      </div>

      <MenuCostBar
        preview={preview}
        onSave={handleSave}
        isSaving={isSaving}
        canSave={!isSaving}
        saveLabel={isEdit ? 'Save Changes' : 'Save Recipe'}
      />
    </div>
  )
}
