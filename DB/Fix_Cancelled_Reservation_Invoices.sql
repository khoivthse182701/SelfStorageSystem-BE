-- Repair only unpaid booking invoices whose reservation has already been cancelled.
-- Paid receipts and customer history are preserved. Safe to run repeatedly.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Fixed TABLE (invoice_id BIGINT, invoice_no NVARCHAR(100), previous_status NVARCHAR(30));
UPDATE i
SET status = 'voided', voided_at = SYSUTCDATETIME(), updated_at = SYSUTCDATETIME()
OUTPUT inserted.id, inserted.invoice_no, deleted.status INTO @Fixed
FROM core.invoices i
JOIN core.reservations r ON r.id = i.reservation_id
WHERE i.customer_id = 7
  AND i.invoice_no IN ('INV-20261108-01018', 'INV-20261108-01022')
  AND r.status = 'cancelled'
  AND i.status IN ('draft', 'open', 'overdue')
  AND i.paid_amount = 0
  AND NOT EXISTS (SELECT 1 FROM core.payments p WHERE p.target_invoice_id = i.id AND p.status IN ('succeeded', 'refunded', 'partially_refunded'))
  AND NOT EXISTS (SELECT 1 FROM core.payment_allocations a WHERE a.invoice_id = i.id);

UPDATE p
SET status = 'cancelled', failure_reason = N'Reservation cancelled; unpaid invoice voided.', updated_at = SYSUTCDATETIME()
FROM core.payments p JOIN @Fixed f ON f.invoice_id = p.target_invoice_id
WHERE p.status = 'pending';

SELECT invoice_no, previous_status, 'voided' AS current_status FROM @Fixed;
COMMIT;
