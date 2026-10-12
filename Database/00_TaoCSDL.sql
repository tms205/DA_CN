/* =============================================================
   DE TAI: HE THONG QUAN LY DICH VU PHOTOCOPY TRUC TUYEN
   FILE 00: TAO CO SO DU LIEU
   ============================================================= */

USE master;
GO

IF DB_ID('PhotocopyChain') IS NOT NULL
BEGIN
    ALTER DATABASE PhotocopyChain SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PhotocopyChain;
END
GO

CREATE DATABASE PhotocopyChain COLLATE Vietnamese_CI_AS;
GO

USE PhotocopyChain;
GO
