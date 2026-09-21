-- Seed fiktif KosManager (data contoh, bukan data nyata).
INSERT INTO Users (Email, PasswordHash, Role, CreatedAt) VALUES
('owner@kos.local', '$2b$12$QEKer6ijBqTb8R0CgAKIoONq.iz97eSt537Ds/no0Erruxs2RBIUW', 'owner', UTC_TIMESTAMP()),
('sinta@kos.local', '$2b$12$Yli.DCPybFStsMaXwJFiEuArHF6YihXP4KVh5dH7S2mq8byjQH1nW', 'penghuni', UTC_TIMESTAMP());

INSERT INTO Rooms (Number, Type, MonthlyPrice, Status) VALUES
('A1', 'standar', 850000, 'isi'),
('A2', 'standar', 850000, 'isi'),
('A3', 'deluxe', 1200000, 'isi'),
('B1', 'standar', 800000, 'isi'),
('B2', 'deluxe', 1200000, 'isi'),
('B3', 'standar', 800000, 'kosong');

INSERT INTO Tenants (Name, Phone, TelegramChatId, UserId, RoomId, MoveInDate) VALUES
('Sinta Prabowo', '0812000011', NULL, 2, 1, '2025-01-10'),
('Rudi Hartono', '0812000012', NULL, NULL, 2, '2025-02-01'),
('Maya Citra', '0812000013', NULL, NULL, 3, '2025-03-15'),
('Doni Saputra', '0812000014', NULL, NULL, 4, '2024-11-20'),
('Lina Marlina', '0812000015', NULL, NULL, 5, '2025-06-05');

INSERT INTO Bills (TenantId, Period, Amount, DueDate, Status, RemindedStage) VALUES
(1, '2026-08', 850000, '2026-08-10', 'paid', 2),
(2, '2026-08', 850000, '2026-08-10', 'paid', 2),
(3, '2026-08', 1200000, '2026-08-10', 'paid', 2),
(1, '2026-09', 850000, '2026-09-10', 'unpaid', 0),
(2, '2026-09', 850000, '2026-09-10', 'pending', 2),
(3, '2026-09', 1200000, '2026-09-10', 'unpaid', 0),
(4, '2026-09', 800000, '2026-09-10', 'unpaid', 0);
