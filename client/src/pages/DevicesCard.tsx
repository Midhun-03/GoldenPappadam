import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Smartphone } from 'lucide-react'
import { toast } from 'sonner'
import { devicesApi } from '@/api/fieldsales'
import { stockApi } from '@/api/inventory'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDateTime } from '@/lib/format'

const NO_VAN = 'none'

/**
 * Which van each phone rides in.
 *
 * This is not decoration: the server reads the van off the device rather than trusting anything the
 * phone sends, so a salesperson whose handset has no van cannot record a load at all. It is the one
 * setting that has to be made before the van screen on their phone does anything.
 */
export function DevicesCard() {
  const queryClient = useQueryClient()

  const devices = useQuery({ queryKey: ['devices'], queryFn: () => devicesApi.list() })
  const locations = useQuery({ queryKey: ['stock', 'locations'], queryFn: stockApi.locations })
  const vans = (locations.data ?? []).filter((location) => location.kind === 'Van')

  const setVan = useMutation({
    mutationFn: ({ id, locationId }: { id: string; locationId: string | null }) =>
      devicesApi.setVan(id, locationId),
    onSuccess: async (device) => {
      await queryClient.invalidateQueries({ queryKey: ['devices'] })
      toast.success(
        device.vanName ? `${device.name} is on ${device.vanName}.` : `${device.name} has no van.`,
      )
    },
    onError: (caught) =>
      toast.error(caught instanceof ApiError ? caught.message : 'Could not change the phone.'),
  })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      devicesApi.setActive(id, isActive),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['devices'] })
      toast.success('Phone updated')
    },
    onError: (caught) =>
      toast.error(caught instanceof ApiError ? caught.message : 'Could not change the phone.'),
  })

  const unassigned = (devices.data ?? []).filter((device) => device.locationId === null)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Phones</CardTitle>
        <CardDescription>
          Which van each phone rides in. A phone with no van cannot record what it took from the
          warehouse.
        </CardDescription>
      </CardHeader>

      <CardContent className="px-0">
        {unassigned.length > 0 && (
          <div className="mx-6 mb-3 flex items-start gap-2.5 rounded-lg border border-warning/30 bg-warning/5 px-3 py-2.5 text-sm text-warning">
            <AlertTriangle className="mt-0.5 size-4 shrink-0" />
            <p>
              {unassigned.length === 1
                ? `${unassigned[0].name} has no van yet, so it cannot record a load.`
                : `${unassigned.length} phones have no van yet, so they cannot record a load.`}
            </p>
          </div>
        )}

        {devices.isPending ? (
          <p className="px-6 text-sm text-muted-foreground">Loading…</p>
        ) : devices.isError ? (
          <ErrorState error={devices.error} />
        ) : devices.data.length === 0 ? (
          <EmptyState
            icon={Smartphone}
            title="No phones yet"
            description="A phone appears here the first time a salesperson signs in on it."
          />
        ) : (
          <Table>
            <TableHeader sticky>
              <TableRow>
                <TableHead>Phone</TableHead>
                <TableHead className="w-44">Van</TableHead>
                <TableHead className="w-24 text-right" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {devices.data.map((device) => (
                <TableRow key={device.id}>
                  <TableCell className="max-w-[12rem]">
                    <div className="truncate font-medium">{device.salesperson}</div>
                    <div className="truncate text-xs text-muted-foreground">
                      {device.name} · last seen {formatDateTime(device.lastSeenAt)}
                    </div>
                  </TableCell>
                  <TableCell>
                    <Select
                      value={device.locationId ?? NO_VAN}
                      disabled={setVan.isPending}
                      onValueChange={(value) =>
                        setVan.mutate({ id: device.id, locationId: value === NO_VAN ? null : value })
                      }
                    >
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={NO_VAN}>No van</SelectItem>
                        {vans.map((van) => (
                          <SelectItem key={van.id} value={van.id}>
                            {van.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {device.locationId === null && (
                      <Badge variant="warning" className="mt-1">
                        Cannot load
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-right">
                    <Button
                      size="sm"
                      variant="ghost"
                      disabled={setActive.isPending}
                      onClick={() =>
                        setActive.mutate({ id: device.id, isActive: !device.isActive })
                      }
                    >
                      {device.isActive ? 'Block' : 'Allow'}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}
