-- Reset only this customer's activity. Keep the account, login and display name.
-- Preview: sqlcmd -S localhost -E -C -d SelfStoragePRN222 -b -i DB/Reset_Customer_yu9696000.sql -v CommitReset=0
-- Apply:   use the same command with -v CommitReset=1
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
DECLARE @Commit bit = $(CommitReset);
DECLARE @Email nvarchar(255) = N'yu9696000@gmail.com';
BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME() <> N'SelfStoragePRN222' THROW 51000, 'Unexpected database.', 1;
    DECLARE @UserId bigint;
    SELECT @UserId=id FROM core.users WITH (UPDLOCK,HOLDLOCK) WHERE email=@Email;
    IF @UserId IS NULL THROW 51000, 'Customer not found.', 1;
    IF EXISTS (SELECT 1 FROM core.employee_profiles WHERE user_id=@UserId)
        THROW 51000, 'Expected a customer account, not an employee.', 1;

    CREATE TABLE #Target (table_name sysname NOT NULL, id bigint NOT NULL, PRIMARY KEY(table_name,id));
    CREATE TABLE #Counts (table_name sysname PRIMARY KEY, deleted_rows int NOT NULL);
    DECLARE @Table sysname, @Column sysname, @Parent sysname, @Sql nvarchar(max), @Added int=1;
    DECLARE roots CURSOR LOCAL FAST_FORWARD FOR
        SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
        WHERE s.name='core' AND EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=t.object_id AND c.name='customer_id')
        AND EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=t.object_id AND c.name='id');
    OPEN roots; FETCH NEXT FROM roots INTO @Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'INSERT #Target SELECT @t,id FROM core.'+QUOTENAME(@Table)+N' WHERE customer_id=@u;';
        EXEC sys.sp_executesql @Sql,N'@t sysname,@u bigint',@Table,@UserId;
        FETCH NEXT FROM roots INTO @Table;
    END;
    CLOSE roots; DEALLOCATE roots;
    INSERT #Target SELECT 'notifications',id FROM core.notifications WHERE user_id=@UserId;
    INSERT #Target SELECT 'login_history',id FROM core.login_history WHERE user_id=@UserId OR attempted_email=@Email;
    INSERT #Target SELECT 'audit_logs',id FROM core.audit_logs WHERE actor_user_id=@UserId;

    -- Legacy seed tasks can refer to an agreement only by its unique number.
    INSERT #Target SELECT 'staff_tasks',s.id FROM core.staff_tasks s
    WHERE EXISTS(SELECT 1 FROM core.rental_agreements a WHERE a.customer_id=@UserId
        AND a.facility_id=s.facility_id AND CHARINDEX(a.agreement_no,s.title)>0);

    -- Follow foreign keys from the selected business records to their children.
    WHILE @Added>0
    BEGIN
        SET @Added=0;
        DECLARE edges CURSOR LOCAL FAST_FORWARD FOR
            SELECT ct.name,cc.name,pt.name
            FROM sys.foreign_key_columns f
            JOIN sys.tables ct ON ct.object_id=f.parent_object_id
            JOIN sys.schemas cs ON cs.schema_id=ct.schema_id
            JOIN sys.columns cc ON cc.object_id=ct.object_id AND cc.column_id=f.parent_column_id
            JOIN sys.tables pt ON pt.object_id=f.referenced_object_id
            JOIN sys.columns pc ON pc.object_id=pt.object_id AND pc.column_id=f.referenced_column_id
            WHERE cs.name='core' AND pc.name='id'
            AND EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=ct.object_id AND c.name='id');
        OPEN edges; FETCH NEXT FROM edges INTO @Table,@Column,@Parent;
        WHILE @@FETCH_STATUS=0
        BEGIN
            SET @Sql=N'INSERT #Target SELECT @t,c.id FROM core.'+QUOTENAME(@Table)+N' c
                WHERE EXISTS(SELECT 1 FROM #Target p WHERE p.table_name=@p AND p.id=c.'+QUOTENAME(@Column)+N')
                AND NOT EXISTS(SELECT 1 FROM #Target x WHERE x.table_name=@t AND x.id=c.id); SET @n=@@ROWCOUNT;';
            DECLARE @N int;
            EXEC sys.sp_executesql @Sql,N'@t sysname,@p sysname,@n int OUTPUT',@Table,@Parent,@N OUTPUT;
            SET @Added+=@N;
            FETCH NEXT FROM edges INTO @Table,@Column,@Parent;
        END;
        CLOSE edges; DEALLOCATE edges;
    END;

    -- Reject inconsistent ownership instead of removing another customer's records.
    DECLARE owners CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT t.table_name FROM #Target t JOIN sys.tables st ON st.name=t.table_name
        JOIN sys.schemas ss ON ss.schema_id=st.schema_id AND ss.name='core'
        WHERE EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=st.object_id AND c.name='customer_id');
    OPEN owners; FETCH NEXT FROM owners INTO @Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'IF EXISTS(SELECT 1 FROM core.'+QUOTENAME(@Table)+N' c JOIN #Target t ON t.table_name=@t AND t.id=c.id WHERE c.customer_id<>@u) THROW 51000,''Cross-customer reference detected.'',1;';
        EXEC sys.sp_executesql @Sql,N'@t sysname,@u bigint',@Table,@UserId;
        FETCH NEXT FROM owners INTO @Table;
    END;
    CLOSE owners; DEALLOCATE owners;

    SELECT DISTINCT storage_unit_id AS id INTO #Units FROM core.unit_allocations a
    WHERE EXISTS(SELECT 1 FROM #Target t WHERE t.table_name='unit_allocations' AND t.id=a.id)
    UNION SELECT from_unit_id FROM core.unit_transfer_requests r JOIN #Target t ON t.table_name='unit_transfer_requests' AND t.id=r.id
    UNION SELECT to_unit_id FROM core.unit_transfer_requests r JOIN #Target t ON t.table_name='unit_transfer_requests' AND t.id=r.id WHERE to_unit_id IS NOT NULL
    UNION SELECT storage_unit_id FROM core.inspections i JOIN #Target t ON t.table_name='inspections' AND t.id=i.id
    UNION SELECT storage_unit_id FROM core.support_tickets s JOIN #Target t ON t.table_name='support_tickets' AND t.id=s.id WHERE storage_unit_id IS NOT NULL;

    INSERT #Target SELECT 'audit_logs',a.id FROM core.audit_logs a
    WHERE NOT EXISTS(SELECT 1 FROM #Target t WHERE t.table_name='audit_logs' AND t.id=a.id)
    AND ((a.entity_type IN ('User','CustomerProfile','users','customer_profiles') AND a.entity_id=CONVERT(nvarchar(30),@UserId))
        OR EXISTS(SELECT 1 FROM #Target t WHERE a.entity_id=CONVERT(nvarchar(30),t.id)
            AND LOWER(REPLACE(a.entity_type,'_','')) IN (REPLACE(t.table_name,'_',''),LEFT(REPLACE(t.table_name,'_',''),LEN(REPLACE(t.table_name,'_',''))-1))));

    INSERT #Target SELECT 'unit_status_history',h.id FROM core.unit_status_history h
    WHERE h.changed_by=@UserId OR EXISTS(SELECT 1 FROM core.rental_agreements a
        WHERE a.customer_id=@UserId AND CHARINDEX(a.agreement_no,h.reason)>0);
    INSERT #Target SELECT 'integration_events',e.id FROM core.integration_events e
    WHERE EXISTS(SELECT 1 FROM OPENJSON(e.payload) j JOIN #Target t ON t.id=TRY_CONVERT(bigint,j.value)
        WHERE (j.[key]='renewalId' AND t.table_name='rental_renewals')
           OR (j.[key]='agreementId' AND t.table_name='rental_agreements')
           OR (j.[key]='reservationId' AND t.table_name='reservations')
           OR (j.[key]='paymentId' AND t.table_name='payments')
           OR (j.[key]='invoiceId' AND t.table_name='invoices'))
        OR JSON_VALUE(e.payload,'$.customerId')=CONVERT(nvarchar(30),@UserId)
        OR JSON_VALUE(e.payload,'$.email')=@Email;
    INSERT #Target SELECT 'notifications',n.id FROM core.notifications n
    WHERE NOT EXISTS(SELECT 1 FROM #Target t WHERE t.table_name='notifications' AND t.id=n.id)
        AND EXISTS(SELECT 1 FROM OPENJSON(n.payload) j JOIN #Target t ON t.id=TRY_CONVERT(bigint,j.value)
            WHERE (j.[key]='agreementId' AND t.table_name='rental_agreements')
               OR (j.[key]='reservationId' AND t.table_name='reservations')
               OR (j.[key]='refundId' AND t.table_name='refunds')
               OR (j.[key]='ticketId' AND t.table_name='support_tickets'));
    SELECT table_name,COUNT(*) AS selected_rows FROM #Target GROUP BY table_name ORDER BY table_name;

    -- Temporarily bypass financial immutability during this explicit reset.
    -- DDL is transactional; rollback restores the original trigger states.
    SELECT tr.name,s.name schema_name,t.name table_name INTO #Triggers
    FROM sys.triggers tr JOIN sys.tables t ON t.object_id=tr.parent_id JOIN sys.schemas s ON s.schema_id=t.schema_id
    WHERE tr.is_disabled=0 AND s.name='core' AND (t.name IN (SELECT table_name FROM #Target)
        OR t.name IN ('payment_allocations','invoice_lines','storage_units'));
    DECLARE @Trigger sysname;
    DECLARE triggers_off CURSOR LOCAL FAST_FORWARD FOR SELECT name,table_name FROM #Triggers;
    OPEN triggers_off; FETCH NEXT FROM triggers_off INTO @Trigger,@Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'DISABLE TRIGGER core.'+QUOTENAME(@Trigger)+N' ON core.'+QUOTENAME(@Table); EXEC(@Sql);
        FETCH NEXT FROM triggers_off INTO @Trigger,@Table;
    END;
    CLOSE triggers_off; DEALLOCATE triggers_off;

    DELETE FROM core.refund_approvals WHERE refund_id IN (SELECT id FROM #Target WHERE table_name='refunds');
    INSERT #Counts VALUES('refund_approvals',@@ROWCOUNT);
    DELETE FROM core.ticket_charge_approvals WHERE proposal_id IN (SELECT id FROM #Target WHERE table_name='ticket_charge_proposals');
    INSERT #Counts VALUES('ticket_charge_approvals',@@ROWCOUNT);
    DELETE FROM core.service_ratings WHERE customer_id=@UserId OR ticket_id IN (SELECT id FROM #Target WHERE table_name='support_tickets');
    INSERT #Counts VALUES('service_ratings',@@ROWCOUNT);
    DELETE FROM core.payment_allocations WHERE payment_id IN (SELECT id FROM #Target WHERE table_name='payments') OR invoice_id IN (SELECT id FROM #Target WHERE table_name='invoices');
    INSERT #Counts VALUES('payment_allocations',@@ROWCOUNT);

    DECLARE deletes CURSOR LOCAL FAST_FORWARD FOR SELECT value FROM STRING_SPLIT(
        'ticket_attachments,ticket_messages,ticket_assignments,staff_tasks,maintenance_work_orders,service_ratings,refund_approvals,ticket_charge_approvals,payment_allocations,access_events,access_credentials,authorized_access_members,handover_records,inspection_items,unit_transfer_requests,rental_renewals,move_out_requests,delinquency_actions,delinquency_cases,refunds,payments,promotion_redemptions,invoice_lines,invoices,ticket_charge_proposals,support_tickets,inspections,identity_verifications,appointments,stored_items,unit_allocations,rental_agreements,reservations,notifications,login_history,audit_logs,unit_status_history,integration_events',',',1) ORDER BY ordinal;
    OPEN deletes; FETCH NEXT FROM deletes INTO @Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF EXISTS(SELECT 1 FROM #Target WHERE table_name=@Table)
        BEGIN
            SET @Sql=N'DELETE c FROM core.'+QUOTENAME(@Table)+N' c JOIN #Target t ON t.table_name=@t AND t.id=c.id; INSERT #Counts VALUES(@t,@@ROWCOUNT);';
            EXEC sys.sp_executesql @Sql,N'@t sysname',@Table;
        END;
        FETCH NEXT FROM deletes INTO @Table;
    END;
    CLOSE deletes; DEALLOCATE deletes;

    IF EXISTS(SELECT 1 FROM core.unit_allocations a JOIN #Units u ON u.id=a.storage_unit_id WHERE a.status='active')
        THROW 51000,'An affected unit still has another active allocation.',1;
    IF EXISTS(SELECT 1 FROM core.maintenance_work_orders m JOIN #Units u ON u.id=m.storage_unit_id WHERE m.blocks_booking=1 AND m.status NOT IN ('completed','cancelled'))
        THROW 51000,'An affected unit has unrelated blocking maintenance.',1;
    UPDATE s SET physical_status='available',is_listed=1,updated_at=SYSUTCDATETIME()
    FROM core.storage_units s JOIN #Units u ON u.id=s.id;

    UPDATE core.customer_profiles SET identity_number=NULL,date_of_birth=NULL,address=NULL,
        emergency_contact_name=NULL,emergency_contact_phone=NULL,updated_at=SYSUTCDATETIME() WHERE user_id=@UserId;
    UPDATE core.users SET last_login_at=NULL,updated_at=SYSUTCDATETIME() WHERE id=@UserId;

    DECLARE triggers_on CURSOR LOCAL FAST_FORWARD FOR SELECT name,table_name FROM #Triggers;
    OPEN triggers_on; FETCH NEXT FROM triggers_on INTO @Trigger,@Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'ENABLE TRIGGER core.'+QUOTENAME(@Trigger)+N' ON core.'+QUOTENAME(@Table); EXEC(@Sql);
        FETCH NEXT FROM triggers_on INTO @Trigger,@Table;
    END;
    CLOSE triggers_on; DEALLOCATE triggers_on;
    SELECT * FROM #Counts WHERE deleted_rows>0 ORDER BY table_name;
    SELECT s.id,s.unit_code,s.physical_status,s.is_listed FROM core.storage_units s JOIN #Units u ON u.id=s.id;
    IF @Commit=1 BEGIN COMMIT; PRINT 'RESET COMMITTED'; END
    ELSE BEGIN ROLLBACK; PRINT 'PREVIEW ONLY - ROLLED BACK'; END;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
