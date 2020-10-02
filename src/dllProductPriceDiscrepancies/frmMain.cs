using Nwuram.Framework.Settings.Connection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dllProductPriceDiscrepancies
{
    public partial class frmMain : Form
    {
        private DataTable dtDeps, dtGrp1, dtGrp2, dtData;
        private bool isLoadData = false;

        public frmMain()
        {
            InitializeComponent();
            dgvData.AutoGenerateColumns = false;

            if (Config.hCntMain == null)
                Config.hCntMain = new Procedures(ConnectionSettings.GetServer(), ConnectionSettings.GetDatabase(), ConnectionSettings.GetUsername(), ConnectionSettings.GetPassword(), ConnectionSettings.ProgramName);


            if (Config.hCntMainKassRealizz == null)
                Config.hCntMainKassRealizz = new Procedures(ConnectionSettings.GetServer("2"), ConnectionSettings.GetDatabase("2"), ConnectionSettings.GetUsername(), ConnectionSettings.GetPassword(), ConnectionSettings.ProgramName);

            if (Config.hCntSecond == null)
                Config.hCntSecond = new Procedures(ConnectionSettings.GetServer("3"), ConnectionSettings.GetDatabase("3"), ConnectionSettings.GetUsername(), ConnectionSettings.GetPassword(), ConnectionSettings.ProgramName);

            if (Config.hCntSecondKassRealizz == null)
                Config.hCntSecondKassRealizz = new Procedures(ConnectionSettings.GetServer("4"), ConnectionSettings.GetDatabase("4"), ConnectionSettings.GetUsername(), ConnectionSettings.GetPassword(), ConnectionSettings.ProgramName);
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

            Task<DateTime> taskTime = Config.hCntMain.getDate();
            taskTime.Wait();
            DateTime nowDate = taskTime.Result;
            dtpStart.Value = nowDate.AddDays(-8);
            dtpStart.Value = nowDate.AddDays(-8);

            cDelta.HeaderText = $"Разница\n(по модулю)";
            Task<DataTable> task = Config.hCntMain.getDepartments(true);
            task.Wait();

            dtDeps = task.Result;

            cmbDeps.DataSource = dtDeps;
            cmbDeps.DisplayMember = "cName";
            cmbDeps.ValueMember = "id";
            

            task = Config.hCntMain.getGrp1(true);
            task.Wait();
            dtGrp1 = task.Result;

            task = Config.hCntMain.getGrp2(true);
            task.Wait();
            dtGrp2 = task.Result;

            cmbDeps_SelectionChangeCommitted(null, null);
            getData();
        }

        private void cmbDeps_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (dtGrp1 != null)
            {
                cmbGrp1.DataSource = dtGrp1.AsEnumerable()
                    .Where(r => r.Field<int>("id_otdel") == (int)cmbDeps.SelectedValue || r.Field<int>("id_otdel") == 0)
                    .OrderBy(r => r.Field<int>("isMain"))
                    .ThenBy(r => r.Field<string>("cName"))
                    .CopyToDataTable();
                cmbGrp1.DisplayMember = "cName";
                cmbGrp1.ValueMember = "id";
            }


            if (dtGrp2 != null)
            {
                cmbGrp2.DataSource = dtGrp2.AsEnumerable()
                    .Where(r => r.Field<int>("id_otdel") == (int)cmbDeps.SelectedValue || r.Field<int>("id_otdel") == 0)
                    .OrderBy(r => r.Field<int>("isMain"))
                    .ThenBy(r => r.Field<string>("cName"))
                    .CopyToDataTable();
                cmbGrp2.DisplayMember = "cName";
                cmbGrp2.ValueMember = "id";
            }

            setFilter();
        }

        private void cmbGrp1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            cmbGrp2.SelectedIndex = 0;
            setFilter();
        }

        private void cmbGrp2_SelectionChangeCommitted(object sender, EventArgs e)
        {
            cmbGrp1.SelectedIndex = 0;
            setFilter();
        }

        private DateTime? dateStart, dateEnd;
        private Nwuram.Framework.UI.Service.EnableControlsServiceInProg blockers = new Nwuram.Framework.UI.Service.EnableControlsServiceInProg();
        private async void getData()
        {
            dgvData.DataSource = null;

            if (isLoadData) return;

            dateStart = null;
            dateEnd = null;

            if (chbPeriod.Checked)
            {
                dateStart = dtpStart.Value.Date;
                dateEnd = dtpEnd.Value.Date;
            }


            blockers.SaveControlsEnabledState(this);
            blockers.SetControlsEnabled(this, false);

            var result = await Task<bool>.Factory.StartNew(() =>
            {
                isLoadData = true;
                Config.DoOnUIThread(() =>
                {
                    toolStripProgressBar1.Visible = true;
                }, this);
                Task<DataTable> task = Config.hCntMain.getGoodsWithPromo(dateStart, dateEnd);
                task.Wait();

                dtData = task.Result;

                if (dtData == null || dtData.Rows.Count == 0)
                {
                    Config.DoOnUIThread(() =>
                    {
                        toolStripProgressBar1.Visible = false;
                        blockers.RestoreControlEnabledState(this);
                    }, this);

                    isLoadData = false;
                    return false;
                }

                if (!dtData.Columns.Contains("priceK21"))
                { dtData.Columns.Add(new DataColumn("priceK21", typeof(decimal)) { DefaultValue = 0.00 }); }

                if (!dtData.Columns.Contains("priceX14"))
                { dtData.Columns.Add(new DataColumn("priceX14", typeof(decimal)) { DefaultValue = 0.00 }); }

                if (!dtData.Columns.Contains("delta"))
                { dtData.Columns.Add(new DataColumn("delta", typeof(decimal)) { DefaultValue = 0.00 }); }

                if (!dtData.Columns.Contains("cName"))
                { dtData.Columns.Add(new DataColumn("cName", typeof(string)) { DefaultValue = "" }); }


                DataTable dtGoods, dtGoodsX14;
                task = Config.hCntMainKassRealizz.getGoodUpdates();
                task.Wait();
                dtGoods = task.Result;

                task = Config.hCntSecondKassRealizz.getGoodUpdates();
                task.Wait();

                dtGoodsX14 = task.Result;

                if (dtGoods != null && dtGoods.Rows.Count > 0)
                {
                    DataTable dtTmp = dtData.Clone();
                    var query = (from g in dtData.AsEnumerable()
                                 join k in dtGoods.AsEnumerable() on new { Q = g.Field<string>("ean") } equals new { Q = k.Field<string>("ean") } into t1
                                 join x in dtGoodsX14.AsEnumerable() on new { Q = g.Field<string>("ean") } equals new { Q = x.Field<string>("ean") } into t2
                                 from leftjoin1 in t1.DefaultIfEmpty()
                                 from leftjoin2 in t2.DefaultIfEmpty()
                                 select dtTmp.LoadDataRow(new object[]
                                                  {
                                                  g.Field<string>("ean"),
                                                  g.Field<int>("id_otdel"),
                                                  g.Field<int>("id_grp1"),
                                                  g.Field<int>("id_grp2"),
                                                  g.Field<int>("id_tovar"),
                                                  g.Field<decimal>("rcena"),
                                                  g.Field<bool>("isPromo"),
                                                  g.Field<int>("ntypetovar"),
                                                  g.Field<string>("nameDep"),
                                                  leftjoin1 == null ? 0 : leftjoin1.Field<decimal>("price"),
                                                  leftjoin2 == null ? 0 : leftjoin2.Field<decimal>("price"),
                                                  Math.Abs((leftjoin1 == null ? (decimal)0 : leftjoin1.Field<decimal>("price")) - (leftjoin2 == null ? (decimal)0 : leftjoin2.Field<decimal>("price"))),
                                                  leftjoin1 == null ? "" : leftjoin1.Field<string>("cName")

                                                  }, false));


                    dtData = query.Where(r => r.Field<decimal>("delta") != 0).CopyToDataTable();
                }
                isLoadData = false;
                Config.DoOnUIThread(() =>
                {
                    toolStripProgressBar1.Visible = false;
                    blockers.RestoreControlEnabledState(this);
                }, this); 
                return true;
            });

            setFilter();
            dgvData.DataSource = dtData;

        }

        private void setFilter()
        {
            if (dtData == null || dtData.Rows.Count == 0)
            {
                //btEdit.Enabled = btDelete.Enabled = false;
                //btPrint.Enabled = btViewCartGoods.Enabled = false;
                return;
            }

            try
            {
                string filter = "";

                if (tbEan.Text.Trim().Length != 0)
                    filter += (filter.Length == 0 ? "" : " and ") + $"ean like '%{tbEan.Text.Trim()}%'";

                if (tbName.Text.Trim().Length != 0)
                    filter += (filter.Length == 0 ? "" : " and ") + $"cName like '%{tbName.Text.Trim()}%'";

                if ((int)cmbDeps.SelectedValue != 0)
                    filter += (filter.Length == 0 ? "" : " and ") + $"id_otdel  = {cmbDeps.SelectedValue}";

                if ((int)cmbGrp1.SelectedValue != 0)
                    filter += (filter.Length == 0 ? "" : " and ") + $"id_grp1  = {cmbGrp1.SelectedValue}";

                if ((int)cmbGrp2.SelectedValue != 0)
                    filter += (filter.Length == 0 ? "" : " and ") + $"id_grp2  = {cmbGrp2.SelectedValue}";

                if (!chbReserv.Checked)
                    filter += (filter.Length == 0 ? "" : " and ") + $"ntypetovar not in (1,3)";

                if (chbDiscount.Checked)
                    filter += (filter.Length == 0 ? "" : " and ") + $"isPromo = 1";

                //if (!chbReserv.Checked)
                //  filter += (filter.Length == 0 ? "" : " and ") + $"isActive = 1";

                dtData.DefaultView.RowFilter = filter;
                dtData.DefaultView.Sort = "id_otdel asc, cName asc";
            }
            catch
            {
                dtData.DefaultView.RowFilter = "id_tovar = -1";
            }
            finally
            {
                //btPrint.Enabled = btViewCartGoods.Enabled =
                //dtData.DefaultView.Count != 0;
                //dgvData_SelectionChanged(null, null);
            }
        }

        private void tbEan_TextChanged(object sender, EventArgs e)
        {
            setFilter();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void btClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btUpdate_Click(object sender, EventArgs e)
        {
            getData();
        }

        private void setWidthColumn(int indexRow, int indexCol, int width, Nwuram.Framework.ToExcelNew.ExcelUnLoad report)
        {
            report.SetColumnWidth(indexRow, indexCol, indexRow, indexCol, width);
        }

        private void btPrint_Click(object sender, EventArgs e)
        {

            Nwuram.Framework.ToExcelNew.ExcelUnLoad report = new Nwuram.Framework.ToExcelNew.ExcelUnLoad();

            int indexRow = 1;

            int maxColumns = 0;

            foreach (DataGridViewColumn col in dgvData.Columns)
                if (col.Visible)
                {
                    maxColumns++;
                    /*if (col.Name.Equals(cDeps.Name)) setWidthColumn(indexRow, maxColumns, 18, report);
                    if (col.Name.Equals(cPost.Name)) setWidthColumn(indexRow, maxColumns, 18, report);
                    if (col.Name.Equals(cFIO.Name)) setWidthColumn(indexRow, maxColumns, 20, report);
                    if (col.Name.Equals(cPass.Name)) setWidthColumn(indexRow, maxColumns, 15, report);
                    if (col.Name.Equals(cDatePrintPass.Name)) setWidthColumn(indexRow, maxColumns, 17, report);
                    if (col.Name.Equals(cPhone.Name)) setWidthColumn(indexRow, maxColumns, 17, report);*/
                }

            #region "Head"
            report.Merge(indexRow, 1, indexRow, maxColumns);
            report.AddSingleValue($"{this.Text}", indexRow, 1);
            report.SetFontBold(indexRow, 1, indexRow, 1);
            report.SetFontSize(indexRow, 1, indexRow, 1, 16);
            report.SetCellAlignmentToCenter(indexRow, 1, indexRow, 1);
            indexRow++;
            indexRow++;


            //report.Merge(indexRow, 1, indexRow, maxColumns);
            //report.AddSingleValue($"Отдел: {cmbDeps.Text}", indexRow, 1);
            //indexRow++;

            //report.Merge(indexRow, 1, indexRow, maxColumns);
            //report.AddSingleValue($"Должность: {cmbPost.Text}", indexRow, 1);
            //indexRow++;

            //report.Merge(indexRow, 1, indexRow, maxColumns);
            //report.AddSingleValue($"Место работы: {(rbOffice.Checked ? rbOffice.Text : rbUni.Text)}", indexRow, 1);
            //indexRow++;

            //report.Merge(indexRow, 1, indexRow, maxColumns);
            //report.AddSingleValue($"Статус сотрудника: {(rbWork.Checked ? rbWork.Text : rbUnemploy.Text)}", indexRow, 1);
            //indexRow++;

            //if (tbPostName.Text.Trim().Length != 0 || tbKadrName.Text.Trim().Length != 0)
            //{
            //    report.Merge(indexRow, 1, indexRow, maxColumns);
            //    report.AddSingleValue($"Фильтр: {(tbPostName.Text.Trim().Length != 0 ? $"Должность:{tbPostName.Text.Trim()} | " : "")} {(tbKadrName.Text.Trim().Length != 0 ? $"ФИО:{tbKadrName.Text.Trim()}" : "")}", indexRow, 1);
            //    indexRow++;
            //}

            report.Merge(indexRow, 1, indexRow, maxColumns);
            report.AddSingleValue("Выгрузил: " + Nwuram.Framework.Settings.User.UserSettings.User.FullUsername, indexRow, 1);
            indexRow++;

            report.Merge(indexRow, 1, indexRow, maxColumns);
            report.AddSingleValue("Дата выгрузки: " + DateTime.Now.ToString(), indexRow, 1);
            indexRow++;
            indexRow++;
            #endregion

            int indexCol = 0;
            foreach (DataGridViewColumn col in dgvData.Columns)
                if (col.Visible)
                {
                    indexCol++;
                    report.AddSingleValue(col.HeaderText, indexRow, indexCol);
                }
            report.SetFontBold(indexRow, 1, indexRow, maxColumns);
            report.SetBorders(indexRow, 1, indexRow, maxColumns);
            report.SetCellAlignmentToCenter(indexRow, 1, indexRow, maxColumns);
            report.SetCellAlignmentToJustify(indexRow, 1, indexRow, maxColumns);
            report.SetWrapText(indexRow, 1, indexRow, maxColumns);
            indexRow++;

            foreach (DataRowView row in dtData.DefaultView)
            {
                indexCol = 1;
                report.SetWrapText(indexRow, indexCol, indexRow, maxColumns);
                foreach (DataGridViewColumn col in dgvData.Columns)
                {
                    if (col.Visible)
                    {
                        if (row[col.DataPropertyName] is DateTime)
                            report.AddSingleValue(((DateTime)row[col.DataPropertyName]).ToShortDateString(), indexRow, indexCol);
                        else
                           if (row[col.DataPropertyName] is decimal || row[col.DataPropertyName] is double)
                        {
                            report.AddSingleValueObject(row[col.DataPropertyName], indexRow, indexCol);
                            report.SetFormat(indexRow, indexCol, indexRow, indexCol, "0.00");
                        }
                        else
                            report.AddSingleValue(row[col.DataPropertyName].ToString(), indexRow, indexCol);

                        indexCol++;
                    }
                }

                report.SetBorders(indexRow, 1, indexRow, maxColumns);
                report.SetCellAlignmentToCenter(indexRow, 1, indexRow, maxColumns);
                report.SetCellAlignmentToJustify(indexRow, 1, indexRow, maxColumns);

                indexRow++;
            }
            report.SetColumnAutoSize(6, 1, indexRow, maxColumns);
            report.Show();
        }

        private void chbPeriod_Click(object sender, EventArgs e)
        {
            dtpStart.Enabled = dtpEnd.Enabled = chbPeriod.Checked;
            getData();
        }

        private void dtpStart_ValueChanged(object sender, EventArgs e)
        {
            if (dtpStart.Value.Date > dtpEnd.Value.Date)
                dtpEnd.Value = dtpStart.Value.Date;
        }

        private void dtpEnd_ValueChanged(object sender, EventArgs e)
        {
            if (dtpStart.Value.Date > dtpEnd.Value.Date)
                dtpStart.Value = dtpEnd.Value.Date;
        }

        private void chbDiscount_Click(object sender, EventArgs e)
        {
            setFilter();
        }

        private void dgvData_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            int width = 0;
            foreach (DataGridViewColumn col in dgvData.Columns)
            {
                if (!col.Visible) continue;

                if (col.Name.Equals(cEan.Name))
                {
                    tbEan.Location = new Point(dgvData.Location.X + 1 + width, tbEan.Location.Y);
                    tbEan.Size = new Size(cEan.Width, tbEan.Size.Height);
                }

                if (col.Name.Equals(cName.Name))
                {
                    tbName.Location = new Point(dgvData.Location.X + 1 + width, tbEan.Location.Y);
                    tbName.Size = new Size(cName.Width, tbEan.Size.Height);
                }

                width += col.Width;
            }
        }

        private void dgvData_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex != -1 && dtData != null && dtData.DefaultView.Count != 0)
            {
                Color rColor = Color.White;
                if (new List<int>(new int[] { 1, 3 }).Contains((int)dtData.DefaultView[e.RowIndex]["ntypetovar"]))
                {
                    rColor = pReserv.BackColor;
                }
                else
                    if ((bool)dtData.DefaultView[e.RowIndex]["isPromo"])
                {
                    rColor = pPromo.BackColor;
                }
                dgvData.Rows[e.RowIndex].DefaultCellStyle.BackColor = rColor;
                dgvData.Rows[e.RowIndex].DefaultCellStyle.SelectionBackColor = rColor;
                dgvData.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = Color.Black;
            }
        }

        private void dgvData_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            //Рисуем рамку для выделеной строки
            if (dgv.Rows[e.RowIndex].Selected)
            {
                int width = dgv.Width;
                Rectangle r = dgv.GetRowDisplayRectangle(e.RowIndex, false);
                Rectangle rect = new Rectangle(r.X, r.Y, width - 1, r.Height - 1);

                ControlPaint.DrawBorder(e.Graphics, rect,
                    SystemColors.Highlight, 2, ButtonBorderStyle.Solid,
                    SystemColors.Highlight, 2, ButtonBorderStyle.Solid,
                    SystemColors.Highlight, 2, ButtonBorderStyle.Solid,
                    SystemColors.Highlight, 2, ButtonBorderStyle.Solid);
            }
        }
    }
}
