-- ============================================================
-- FIX: Add missing columns to employee_leaves table
-- Run this in phpMyAdmin or MySQL Workbench once, then restart the app.
-- ============================================================

USE yeka_cleaning;

-- Add approved_by if missing
ALTER TABLE employee_leaves
    ADD COLUMN IF NOT EXISTS approved_by INT NULL;

-- Add approved_at if missing
ALTER TABLE employee_leaves
    ADD COLUMN IF NOT EXISTS approved_at DATETIME NULL;

-- Verify the columns were added
SHOW COLUMNS FROM employee_leaves;
