INSERT INTO "Priorities" ("Type")
SELECT 'Low'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Priorities"
    WHERE "Type" = 'Low'
);

INSERT INTO "Priorities" ("Type")
SELECT 'Medium'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Priorities"
    WHERE "Type" = 'Medium'
);

INSERT INTO "Priorities" ("Type")
SELECT 'High'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Priorities"
    WHERE "Type" = 'High'
);

INSERT INTO "Priorities" ("Type")
SELECT 'Critical'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Priorities"
    WHERE "Type" = 'Critical'
);
