SET NOCOUNT ON;
UPDATE [core].[users] 
SET [password_hash] = '$2a$11$9O6BGbDeWhCOyK8PW2pFUuRcu4BW9hHDbMG7G4QyCdCERhk7uXTQy',
    [status] = 'active'
WHERE [email] IN ('staff@gmail.com', 'admin@gmail.com');

SELECT id, email, LEN(password_hash) as [len], password_hash, status 
FROM [core].[users] 
WHERE [email] IN ('staff@gmail.com', 'admin@gmail.com');
