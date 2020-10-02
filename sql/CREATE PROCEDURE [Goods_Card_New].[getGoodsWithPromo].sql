SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		Sporykhin G.Y.
-- Create date: 2020-04-25
-- Description:	Получение товаров из goods updates
-- =============================================
ALTER PROCEDURE [Goods_Card_New].[getGoodsWithPromo]		
	@dateStart date = null,
	@dateEnd date = null
AS
BEGIN
	SET NOCOUNT ON;

IF @dateStart is null
	BEGIN
		select 
			ltrim(rtrim(t.ean)) as ean,
			t.id_otdel,
			t.id_grp1,
			t.id_grp2,
			t.id as id_tovar,	
			r.rcena,
			cast(case when c.id is null then 0 else 1 end as bit) as isPromo,
			n.ntypetovar,
			ltrim(rtrim(d.name)) as nameDep
		from 
			dbo.s_tovar t	
			left join dbo.s_ntovar n on n.id_tovar = t.id and n.isActual = 1
			left join dbo.departments d on d.id = t.id_otdel
			left join dbo.s_rcena r on r.id_tovar = t.id and r.isActual = 1
			left join requests.s_CatalogPromotionalTovars c on c.id_tovar = t.id 
	END
ELSE
	BEGIN
		select 
			ltrim(rtrim(t.ean)) as ean,
			t.id_otdel,
			t.id_grp1,
			t.id_grp2,
			t.id as id_tovar,	
			r.rcena,
			cast(case when c.id is null then 0 else 1 end as bit) as isPromo,
			n.ntypetovar,
			ltrim(rtrim(d.name)) as nameDep
		from 
			(select rr.id_tovar from dbo.j_realiz rr where @dateStart<= rr.drealiz and  rr.drealiz <=@dateEnd group by rr.id_tovar) as rr
				inner join dbo.s_tovar t on t.id = rr.id_tovar
				left join dbo.s_ntovar n on n.id_tovar = t.id and n.isActual = 1
				left join dbo.departments d on d.id = t.id_otdel
				left join dbo.s_rcena r on r.id_tovar = rr.id_tovar and r.isActual = 1
				left join requests.s_CatalogPromotionalTovars c on c.id_tovar = t.id		
	END
END
