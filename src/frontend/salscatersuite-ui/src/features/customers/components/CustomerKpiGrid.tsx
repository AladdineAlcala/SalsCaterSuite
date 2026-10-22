import { Building2, Hourglass, TrendingUp, Users } from 'lucide-react'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { CustomerDirectorySummary } from '@/types/customer.types'
import { CustomerKpiCard } from './CustomerKpiCard'

export interface CustomerKpiGridProps {
  summary: CustomerDirectorySummary
}

/**
 * The four KPI cards (spec §4.2).
 *
 * Every number is read from the directory summary the API computed. The
 * average contract value is `null` when there are no clients, and it renders
 * as an em dash — never as a zero (spec §4.2, contract rule 6).
 */
export function CustomerKpiGrid({ summary }: CustomerKpiGridProps) {
  const averageValue =
    summary.averageValuePerClient === null
      ? EM_DASH
      : formatCurrency(summary.averageValuePerClient)

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-gutter">
      {/* 1 — Total Registered Clients (KPI-20) */}
      <CustomerKpiCard
        icon={Users}
        iconTone="neutral"
        label="Total Registered Clients"
        value={formatCount(summary.totalClients)}
        unit="Clients"
        subLine="Profiles Active"
      />

      {/* 2 — Active AR Exposure (KPI-12) */}
      <CustomerKpiCard
        icon={Hourglass}
        iconTone="caramel"
        label="Active AR Exposure"
        value={formatCurrency(summary.activeArAmount)}
        subLine={`${formatCount(summary.clientsWithBalance)} with open balance`}
      />

      {/* 3 — Total Contracted Value (KPI-21) */}
      <CustomerKpiCard
        icon={TrendingUp}
        iconTone="culinary"
        label="Total Contracted Value"
        value={formatCurrency(summary.totalContractedValue)}
        subLine={`Avg ${averageValue} per client`}
      />

      {/* 4 — Overdue AR (KPI-13) */}
      <CustomerKpiCard
        icon={Building2}
        iconTone="caramel"
        label="Overdue AR (>30 days)"
        value={formatCurrency(summary.overdueAmount)}
        subLine={`${formatCount(summary.overdueClientCount)} clients overdue`}
      />
    </div>
  )
}
