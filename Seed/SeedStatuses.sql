INSERT INTO "Statuses" ("Name")
SELECT 'Initiated'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Statuses"
    WHERE "Name" = 'Initiated'
);

INSERT INTO "Statuses" ("Name")
SELECT 'In Progress'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Statuses"
    WHERE "Name" = 'In Progress'
);

INSERT INTO "Statuses" ("Name")
SELECT 'Closed'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Statuses"
    WHERE "Name" = 'Closed'
);

INSERT INTO "Statuses" ("Name")
SELECT 'Cancelled'
WHERE NOT EXISTS (
    SELECT 1
    FROM "Statuses"
    WHERE "Name" = 'Cancelled'
);
