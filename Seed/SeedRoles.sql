INSERT INTO "Roles" ("Name")
SELECT 'Admin'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Roles"
    WHERE "Name" = 'Admin'
);

INSERT INTO "Roles" ("Name")
SELECT 'Officer'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Roles"
    WHERE "Name" = 'Officer'
);
