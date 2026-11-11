CREATE DATABASE IF NOT EXISTS academic CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE academic;
CREATE TABLE courses(id BIGINT PRIMARY KEY, name VARCHAR(255) NOT NULL, synced_at DATETIME(6) NOT NULL, source VARCHAR(40) NOT NULL, activities JSON NOT NULL);
CREATE TABLE students(course_id BIGINT NOT NULL,id BIGINT NOT NULL,name VARCHAR(255) NOT NULL,PRIMARY KEY(course_id,id),FOREIGN KEY(course_id) REFERENCES courses(id));
CREATE TABLE grades(course_id BIGINT NOT NULL,student_id BIGINT NOT NULL,item_id BIGINT NOT NULL,payload JSON NOT NULL,PRIMARY KEY(course_id,student_id,item_id),FOREIGN KEY(course_id,student_id) REFERENCES students(course_id,id));
CREATE TABLE sync_runs(id BIGINT AUTO_INCREMENT PRIMARY KEY,course_id BIGINT NOT NULL,actor_id BIGINT NOT NULL,finished_at DATETIME(6) NOT NULL,status VARCHAR(30) NOT NULL,error_code VARCHAR(80) NULL);
