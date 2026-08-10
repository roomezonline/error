-- Insert 3000 test records for monitoringId=20
DECLARE @i INT = 0;
DECLARE @baseTime DATETIME = DATEADD(HOUR, -72, GETUTCDATE());
DECLARE @deviceCode NVARCHAR(50) = '13';

WHILE @i < 3000
BEGIN
    DECLARE @ts DATETIME = DATEADD(SECOND, @i * 86, @baseTime);
    DECLARE @tempRef FLOAT = 5.0 + 15.0 * SIN(@i * 0.03) + (RAND(CHECKSUM(NEWID())) * 2.0 - 1.0);
    DECLARE @tempFreez FLOAT = -18.0 + 5.0 * SIN(@i * 0.02 + 1.0) + (RAND(CHECKSUM(NEWID())) * 1.0 - 0.5);
    DECLARE @motor BIT = CASE WHEN (@i % 20) < 15 THEN 1 ELSE 0 END;
    DECLARE @bargh BIT = 1;
    DECLARE @element1 BIT = CASE WHEN (@i % 30) < 10 THEN 1 ELSE 0 END;
    DECLARE @element2 BIT = CASE WHEN (@i % 45) < 12 THEN 1 ELSE 0 END;
    DECLARE @fdc1 BIT = CASE WHEN @motor = 1 AND (@i % 5) < 3 THEN 1 ELSE 0 END;
    DECLARE @fac1 BIT = CASE WHEN @motor = 1 AND (@i % 8) < 3 THEN 1 ELSE 0 END;
    DECLARE @jaryan FLOAT = CASE WHEN @motor = 1 THEN 1.5 + RAND(CHECKSUM(NEWID())) * 0.5 ELSE 0.1 END;
    DECLARE @power FLOAT = CASE WHEN @motor = 1 THEN 2.0 + RAND(CHECKSUM(NEWID())) * 0.8 ELSE 0.05 END;
    DECLARE @kw FLOAT = @power * @jaryan * 0.001;
    DECLARE @sumKw FLOAT = @i * 0.05;
    -- Jalali date approximation
    DECLARE @year INT = 1405;
    DECLARE @month INT = 3 + (@i / 300) % 9;
    DECLARE @day INT = 1 + (@i / 100) % 28;
    DECLARE @hour INT = (@i * 86 / 3600) % 24;
    DECLARE @min INT = ((@i * 86) % 3600) / 60;
    DECLARE @sec INT = (@i * 86) % 60;
    DECLARE @timestampFa NVARCHAR(30) = 
        RIGHT('0000' + CAST(@year AS NVARCHAR), 4) + '/' + 
        RIGHT('00' + CAST(@month AS NVARCHAR), 2) + '/' + 
        RIGHT('00' + CAST(@day AS NVARCHAR), 2) + ' ' + 
        RIGHT('00' + CAST(@hour AS NVARCHAR), 2) + ':' + 
        RIGHT('00' + CAST(@min AS NVARCHAR), 2) + ':' + 
        RIGHT('00' + CAST(@sec AS NVARCHAR), 2);

    INSERT INTO MonitoringDataRecords (
        DeviceCode, MonitoringId, CustomerReceiptId,
        TemperatureRef, TemperatureFreez, TemperatureEnv,
        MotorState, Bargh, Element1, Element2, Fdc1, Fac1,
        Jaryan, Power, Kw, SumKw,
        State, Note,
        Timestamp, TimestampFa
    ) VALUES (
        @deviceCode, 20, 17,
        @tempRef, @tempFreez, NULL,
        @motor, @bargh, @element1, @element2, @fdc1, @fac1,
        @jaryan, @power, @kw, @sumKw,
        NULL, NULL,
        @ts, @timestampFa
    );

    SET @i = @i + 1;
END;

-- Update archive record for connection 20
UPDATE MonitoringDataArchives 
SET RecordCount = 3000, EstimatedBytes = 150000, Downloaded = 0, DownloadedAt = NULL
WHERE MonitoringReceiptConnectionId = 20;

SELECT COUNT(*) AS RecordCount FROM MonitoringDataRecords WHERE MonitoringId = 20;
