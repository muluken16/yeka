-- ============================================================
-- Cleanup Script: Remove all seed/demo data
-- Keeps ONLY: superadmin@yeka.et  and  hr@yeka.et
-- Run this in MySQL Workbench or your MySQL client
-- ============================================================

SET FOREIGN_KEY_CHECKS = 0;

-- 1. Delete ALL employee-related records
DELETE FROM employee_performance_reviews;
DELETE FROM employee_payroll;
DELETE FROM employee_attendance;
DELETE FROM employee_leaves;
DELETE FROM employee_documents;

-- 2. Delete ALL employees
DELETE FROM employees;

-- Reset auto-increment
ALTER TABLE employees AUTO_INCREMENT = 1;

-- 3. Delete ALL users EXCEPT superadmin and hr
DELETE FROM users
WHERE email NOT IN ('superadmin@yeka.et', 'hr@yeka.et');

-- Reset auto-increment (optional - comment out if you prefer to keep existing IDs)
-- ALTER TABLE users AUTO_INCREMENT = 1;

-- 4. Clear transport requests (optional - uncomment if needed)
-- DELETE FROM transport_request_logs;
-- DELETE FROM transport_requests;

SET FOREIGN_KEY_CHECKS = 1;

-- Verify what remains
SELECT id, name, email, role, is_active FROM users;
