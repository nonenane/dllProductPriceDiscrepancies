SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		Sporykhin G.Y.
-- Create date: 2020-04-25
-- Description:	Получение товаров из goods updates
-- =============================================
ALTER PROCEDURE [Goods_Card_New].[getGoodUpdates]		 	
AS
BEGIN
	SET NOCOUNT ON;


select 
	ltrim(rtrim(g.ean)) as ean,
	ltrim(rtrim(g.name)) as cName,
	isnull(g.price/100.0,0.0) as price
from 
	dbo.goods_updates g
where
	ActualRow = 1 

END
