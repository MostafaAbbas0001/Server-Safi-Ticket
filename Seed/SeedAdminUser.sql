INSERT INTO "Users" (
    "Name",
    "Email",
    "PhoneNumber",
    "HashedPassword",
    "RoleId"
)
SELECT
    'Mostafa Abbas',
    'mostafa.abbas@saficos.com',
    '',
    'AQAAAAIAAYagAAAAEBQP3ie4rXRuh4NUZgrfaLz+VqLaSpD4SC+LztJ47pyLEsqtDyioTsulQkz8YqgP9w==',
    (SELECT "Id" FROM "Roles" WHERE "Name" = 'Admin' LIMIT 1)
WHERE NOT EXISTS (
    SELECT 1
    FROM "Users"
    WHERE lower("Email") = 'mostafa.abbas@saficos.com'
);
