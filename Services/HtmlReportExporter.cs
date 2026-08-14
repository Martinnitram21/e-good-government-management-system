using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GoodGovernanceApp.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace GoodGovernanceApp.Services;

/// <summary>
/// Generates rich, printable HTML reports for all 16 report types.
/// Opens in the default browser â€” use Ctrl+P to print or Save as PDF.
/// </summary>
public static class HtmlReportExporter
{
    private static readonly string[] Palette = {
        "#3B82F6","#10B981","#F59E0B","#EF4444","#8B5CF6",
        "#06B6D4","#F97316","#84CC16","#EC4899","#6366F1",
        "#0EA5E9","#14B8A6","#D97706","#DC2626","#7C3AED"
    };

    // â”€â”€ Entry point â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static void ExportAndOpen(string reportType, ReportsViewModel vm)
    {
        string html = reportType switch {
            "Financial Overview"                         => BuildFinancialOverview(vm.Financial),
            "Consolidated Transactions Analytics"        => BuildConsolidatedAnalytics(vm.Transaction),
            "CRS Beneficiary Analytics"                  => BuildCrsAnalytics(vm.Beneficiary),
            "User Activity Log"                          => BuildUserActivityLog(vm.SystemReports),
            "Budget Summary by Category"                 => BuildBudgetSummary(vm.Financial),
            "Transaction History"                        => BuildTransactionHistory(vm.Transaction),
            "Parameters List"                            => BuildParametersList(vm.SystemReports),
            "Office Budget Allocation"                   => BuildOfficeBudgetAllocation(vm.Financial),
            "System Overview"                            => BuildSystemOverview(vm.SystemReports),
            "Beneficiaries per Project"                  => BuildBeneficiariesPerProject(vm.Beneficiary),
            "Individual Beneficiaries Services Received" => BuildIndividualBeneficiaries(vm.Beneficiary),
            "Budget Utilization Report"                  => BuildBudgetUtilization(vm.Financial),
            "Project Implementation Status Report"       => BuildProjectStatus(vm.Project),
            "Public Service Delivery Report"             => BuildPublicServiceDelivery(vm.Transaction),
            "Citizen Feedback Summary Report"            => BuildCitizenFeedback(vm.SystemReports),
            "Beneficiary Master List"                    => BuildBeneficiaryMasterList(vm.Beneficiary),
            _                                            => BuildNotAvailable(reportType)
        };
        var safe = string.Concat(reportType.Split(Path.GetInvalidFileNameChars()));
        var tmp  = Path.Combine(Path.GetTempPath(), $"GGMS_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(tmp, html, Encoding.UTF8);
        Process.Start(new ProcessStartInfo(tmp) { UseShellExecute = true });
    }

    // â”€â”€ Utility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static string E(string? s)  => System.Web.HttpUtility.HtmlEncode(s ?? "");
    private static string FC(decimal v) => $"&#8369;{v:N2}";

    // â”€â”€ CSS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static string Css() => @"<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI',-apple-system,Arial,sans-serif;background:#F1F5F9;color:#1E293B;font-size:14px;line-height:1.7}
.page{max-width:960px;margin:32px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 32px rgba(0,0,0,.12)}
.rh{padding:36px 44px 28px;color:#fff;background:linear-gradient(135deg,var(--a1,#1E3A5F) 0%,var(--a2,#2563EB) 100%)}
.rh .org{font-size:11px;letter-spacing:.12em;text-transform:uppercase;opacity:.8}
.rh h1{font-size:26px;font-weight:700;margin:6px 0 4px}
.rh .sub{font-size:13px;opacity:.85}
.rh .meta{font-size:11px;opacity:.65;margin-top:12px}
.bdg{display:inline-block;background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.3);border-radius:20px;padding:3px 12px;font-size:11px;margin-right:8px}
.body{padding:36px 44px}
.exec{background:#EFF6FF;border-left:4px solid #2563EB;border-radius:0 8px 8px 0;padding:18px 20px;margin-bottom:28px}
.exec h3{font-size:12px;color:#1D4ED8;margin-bottom:6px;text-transform:uppercase;letter-spacing:.06em}
.exec p{font-size:14px;color:#1E3A5F}
.sec{margin-bottom:32px}
.sh{display:flex;align-items:center;gap:10px;font-size:15px;font-weight:600;color:#0F172A;border-bottom:2px solid #E2E8F0;padding-bottom:10px;margin-bottom:16px}
.dot{width:8px;height:8px;border-radius:50%;background:var(--a1,#2563EB);flex-shrink:0}
.nar{background:#F8FAFC;border:1px solid #E2E8F0;border-radius:8px;padding:16px 18px;font-size:13.5px;color:#334155;margin-bottom:16px}
.hbox{background:#F0FDF4;border:1px solid #BBF7D0;border-radius:8px;padding:14px 18px;font-size:13px;color:#14532D;margin:14px 0}
.wbox{background:#FFF7ED;border:1px solid #FED7AA;border-radius:8px;padding:14px 18px;font-size:13px;color:#9A3412;margin:14px 0}
.kr{display:grid;gap:14px;margin-bottom:28px}
.k4{grid-template-columns:repeat(4,1fr)}.k3{grid-template-columns:repeat(3,1fr)}.k2{grid-template-columns:repeat(2,1fr)}
.kc{border-radius:10px;padding:18px 20px;color:#fff}
.kl{font-size:12px;opacity:.8;margin-bottom:6px}
.kv{font-size:24px;font-weight:700}
.ks{font-size:11px;opacity:.7;margin-top:4px}
.cr{display:grid;gap:16px;margin-bottom:24px}
.cr2{grid-template-columns:1fr 1fr}
.cc{background:#fff;border:1px solid #E2E8F0;border-radius:10px;padding:20px;overflow:hidden}
.cc h4{font-size:13px;color:#475569;margin-bottom:14px;font-weight:600}
.cc svg{display:block;width:100%}
.tw{overflow-x:auto;border-radius:8px;border:1px solid #E2E8F0}
table{width:100%;border-collapse:collapse;font-size:13px}
thead{background:#1E293B;color:#fff}
thead th{padding:10px 14px;text-align:left;font-weight:500;white-space:nowrap}
tbody tr:nth-child(even){background:#F8FAFC}
tbody tr:hover{background:#EFF6FF}
tbody td{padding:9px 14px;border-bottom:1px solid #E2E8F0}
.pl{display:inline-block;border-radius:20px;padding:2px 10px;font-size:11px;font-weight:600}
.pg{background:#D1FAE5;color:#065F46}.pb{background:#DBEAFE;color:#1E40AF}.po{background:#FEF3C7;color:#92400E}.pr{background:#FEE2E2;color:#991B1B}.pp{background:#EDE9FE;color:#4C1D95}
.prow{display:flex;align-items:center;margin-bottom:10px;gap:12px}
.plb{width:180px;font-size:12px;color:#475569;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.pbar{flex:1;background:#E2E8F0;border-radius:4px;height:14px}
.pfill{height:100%;border-radius:4px}
.pval{width:80px;font-size:12px;color:#334155;text-align:right}
.rf{background:#1E293B;color:#94A3B8;font-size:11px;padding:16px 44px;text-align:center}
@media print{
body{background:#fff!important}
.page{box-shadow:none;margin:0;border-radius:0;max-width:100%}
.no-print{display:none!important}
.page-break{page-break-before:always}
.sec{page-break-inside:avoid}
.kc,.rh,thead,.pfill{-webkit-print-color-adjust:exact;print-color-adjust:exact}
}
</style>";

    // â”€â”€ Page wrapper â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static string Wrap(string title, string sub, string body, string a1="#1E3A5F", string a2="#2563EB")
    {
        var g = DateTime.Now.ToString("MMMM dd, yyyy  hh:mm tt");
        return $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"/><title>GGMS - {E(title)}</title>{Css()}<style>:root{{--a1:{a1};--a2:{a2}}}</style></head><body>"
             + $"<div class=\"page\"><div class=\"rh\"><div class=\"org\">Good Governance Management System &middot; Official Report</div>"
             + $"<h1>{E(title)}</h1><div class=\"sub\">{E(sub)}</div>"
             + $"<div class=\"meta\"><span class=\"bdg\">&#128197; {g}</span><span class=\"bdg\">&#128424; Print: Ctrl+P</span></div></div>"
             + $"<div class=\"body\">{body}</div>"
             + $"<div class=\"rf\">Good Governance Management System &middot; Confidential &middot; {g}</div></div></body></html>";
    }

    // â”€â”€ Component builders â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static string Exec(string t) => $"<div class=\"exec\"><h3>Executive Summary</h3><p>{E(t)}</p></div>";
    private static string Nar(string t)  => $"<div class=\"nar\">{E(t)}</div>";
    private static string Hbox(string t) => $"<div class=\"hbox\">&#10003; {E(t)}</div>";
    private static string Wbox(string t) => $"<div class=\"wbox\">&#9888; {E(t)}</div>";
    private static string Sec(string title, string inner) => $"<div class=\"sec\"><div class=\"sh\"><div class=\"dot\"></div>{E(title)}</div>{inner}</div>";
    private static string KC(string l, string v, string bg) => $"<div class=\"kc\" style=\"background:{bg}\"><div class=\"kl\">{E(l)}</div><div class=\"kv\">{E(v)}</div></div>";

    private static string Tbl(string[] heads, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder("<div class=\"tw\"><table><thead><tr>");
        foreach (var h in heads) sb.Append($"<th>{E(h)}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var r in rows) { sb.Append("<tr>"); foreach (var c in r) sb.Append($"<td>{c}</td>"); sb.Append("</tr>"); }
        return sb.Append("</tbody></table></div>").ToString();
    }

    private static string PBars(IEnumerable<(string L, double V, string C)> items)
    {
        var list = items.ToList();
        double mx = list.Count > 0 ? list.Max(x => x.V) : 1;
        if (mx <= 0) mx = 1;
        var sb = new StringBuilder();
        foreach (var (l, v, c) in list)
            sb.Append($"<div class=\"prow\"><div class=\"plb\">{E(l)}</div><div class=\"pbar\"><div class=\"pfill\" style=\"width:{v/mx*100:F0}%;background:{c}\"></div></div><div class=\"pval\">{v:N0}</div></div>");
        return sb.ToString();
    }

    // â”€â”€ SVG Donut Pie â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static string SvgPie(IEnumerable<(string L, double V)> raw, int w = 340, int h = 260)
    {
        var d = raw.ToList(); double tot = d.Sum(x => x.V);
        if (tot <= 0) return "<p style='color:#94A3B8'>No data.</p>";
        const double cx=110,cy=120,r=95,ir=48;
        var sb = new StringBuilder($"<svg viewBox=\"0 0 {w} {h}\" xmlns=\"http://www.w3.org/2000/svg\">");
        double a = -Math.PI / 2;
        for (int i = 0; i < d.Count; i++) {
            double sw=d[i].V/tot*2*Math.PI, ea=a+sw;
            int lg=sw>Math.PI?1:0; string col=Palette[i%Palette.Length];
            double x1=cx+r*Math.Cos(a),y1=cy+r*Math.Sin(a),x2=cx+r*Math.Cos(ea),y2=cy+r*Math.Sin(ea);
            double xi1=cx+ir*Math.Cos(a),yi1=cy+ir*Math.Sin(a),xi2=cx+ir*Math.Cos(ea),yi2=cy+ir*Math.Sin(ea);
            sb.Append($"<path d=\"M{xi1:F1} {yi1:F1} L{x1:F1} {y1:F1} A{r} {r} 0 {lg} 1 {x2:F1} {y2:F1} L{xi2:F1} {yi2:F1} A{ir} {ir} 0 {lg} 0 {xi1:F1} {yi1:F1}Z\" fill=\"{col}\" stroke=\"#fff\" stroke-width=\"1.5\"/>");
            if (d[i].V/tot > 0.07) {
                double ma=a+sw/2,lr=(r+ir)/2,lx=cx+lr*Math.Cos(ma),ly=cy+lr*Math.Sin(ma);
                sb.Append($"<text x=\"{lx:F0}\" y=\"{ly:F0}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"11\" fill=\"#fff\" font-weight=\"700\">{d[i].V/tot:P0}</text>");
            }
            a = ea;
        }
        sb.Append($"<text x=\"{cx}\" y=\"{cy-7}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#64748B\">Total</text>");
        sb.Append($"<text x=\"{cx}\" y=\"{cy+11}\" text-anchor=\"middle\" font-size=\"13\" fill=\"#1E293B\" font-weight=\"700\">{tot:N0}</text>");
        for (int i = 0; i < d.Count; i++) {
            double lx=230,ly=18+i*22; string col=Palette[i%Palette.Length];
            string lbl=d[i].L.Length>18?d[i].L[..17]+"...":d[i].L;
            sb.Append($"<rect x=\"{lx}\" y=\"{ly}\" width=\"12\" height=\"12\" rx=\"3\" fill=\"{col}\"/>");
            sb.Append($"<text x=\"{lx+17}\" y=\"{ly+10}\" font-size=\"11\" fill=\"#334155\">{E(lbl)} ({d[i].V:N0})</text>");
        }
        return sb.Append("</svg>").ToString();
    }

    // â”€â”€ SVG Bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static string SvgBar(IEnumerable<(string L, double V)> raw, string col="#3B82F6", string yL="", int w=560, int h=260)
    {
        var d = raw.ToList(); if (d.Count == 0) return "<p style='color:#94A3B8'>No data.</p>";
        double mx=d.Max(x=>x.V); if(mx<=0) mx=1;
        double mL=70,mB=54,mT=18,mR=16,cW=w-mL-mR,cH=h-mT-mB,bW=Math.Max(18,cW/d.Count*0.68),st=cW/d.Count;
        var sb = new StringBuilder($"<svg viewBox=\"0 0 {w} {h}\" xmlns=\"http://www.w3.org/2000/svg\">");
        for (int g=0;g<=4;g++) {
            double gy=mT+cH-(g/4.0)*cH,gv=(g/4.0)*mx;
            sb.Append($"<line x1=\"{mL}\" y1=\"{gy:F0}\" x2=\"{w-mR}\" y2=\"{gy:F0}\" stroke=\"#E2E8F0\" stroke-width=\"1\"/>");
            string gs=gv>=1e6?$"{gv/1e6:F1}M":gv>=1000?$"{gv/1000:F0}K":$"{gv:F0}";
            sb.Append($"<text x=\"{mL-4}\" y=\"{gy+4:F0}\" text-anchor=\"end\" font-size=\"9\" fill=\"#94A3B8\">{gs}</text>");
        }
        sb.Append($"<line x1=\"{mL}\" y1=\"{mT}\" x2=\"{mL}\" y2=\"{mT+cH}\" stroke=\"#CBD5E1\" stroke-width=\"1\"/>");
        sb.Append($"<line x1=\"{mL}\" y1=\"{mT+cH}\" x2=\"{w-mR}\" y2=\"{mT+cH}\" stroke=\"#CBD5E1\" stroke-width=\"1\"/>");
        if (!string.IsNullOrEmpty(yL)) sb.Append($"<text x=\"10\" y=\"{mT+cH/2:F0}\" transform=\"rotate(-90 10 {mT+cH/2:F0})\" text-anchor=\"middle\" font-size=\"10\" fill=\"#94A3B8\">{E(yL)}</text>");
        for (int i=0;i<d.Count;i++) {
            double bH=(d[i].V/mx)*cH,bx=mL+i*st+(st-bW)/2,by=mT+cH-bH;
            sb.Append($"<rect x=\"{bx:F0}\" y=\"{by:F0}\" width=\"{bW:F0}\" height=\"{bH:F0}\" fill=\"{col}\" rx=\"3\" opacity=\".9\"/>");
            string vs=d[i].V>=1e6?$"{d[i].V/1e6:F1}M":d[i].V>=1000?$"{d[i].V/1000:F0}K":$"{d[i].V:F0}";
            if(bH>18) sb.Append($"<text x=\"{bx+bW/2:F0}\" y=\"{by+bH/2+4:F0}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#fff\" font-weight=\"700\">{vs}</text>");
            else       sb.Append($"<text x=\"{bx+bW/2:F0}\" y=\"{by-3:F0}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#334155\">{vs}</text>");
            string lb=d[i].L.Length>11?d[i].L[..10]+"...":d[i].L; double lx=bx+bW/2,lya=mT+cH+14;
            sb.Append($"<text x=\"{lx:F0}\" y=\"{lya:F0}\" transform=\"rotate(-35 {lx:F0} {lya:F0})\" text-anchor=\"end\" font-size=\"9\" fill=\"#64748B\">{E(lb)}</text>");
        }
        return sb.Append("</svg>").ToString();
    }

    private static string SvgMBar(IEnumerable<(string L, double V, string C)> raw, string yL="", int w=560, int h=260)
    {
        var d = raw.ToList(); if (d.Count == 0) return "<p style='color:#94A3B8'>No data.</p>";
        double mx=d.Max(x=>x.V); if(mx<=0) mx=1;
        double mL=70,mB=54,mT=18,mR=16,cW=w-mL-mR,cH=h-mT-mB,bW=Math.Max(18,cW/d.Count*0.68),st=cW/d.Count;
        var sb = new StringBuilder($"<svg viewBox=\"0 0 {w} {h}\" xmlns=\"http://www.w3.org/2000/svg\">");
        for (int g=0;g<=4;g++) {
            double gy=mT+cH-(g/4.0)*cH,gv=(g/4.0)*mx;
            sb.Append($"<line x1=\"{mL}\" y1=\"{gy:F0}\" x2=\"{w-mR}\" y2=\"{gy:F0}\" stroke=\"#E2E8F0\" stroke-width=\"1\"/>");
            string gs=gv>=1e6?$"{gv/1e6:F1}M":gv>=1000?$"{gv/1000:F0}K":$"{gv:F0}";
            sb.Append($"<text x=\"{mL-4}\" y=\"{gy+4:F0}\" text-anchor=\"end\" font-size=\"9\" fill=\"#94A3B8\">{gs}</text>");
        }
        sb.Append($"<line x1=\"{mL}\" y1=\"{mT}\" x2=\"{mL}\" y2=\"{mT+cH}\" stroke=\"#CBD5E1\" stroke-width=\"1\"/>");
        sb.Append($"<line x1=\"{mL}\" y1=\"{mT+cH}\" x2=\"{w-mR}\" y2=\"{mT+cH}\" stroke=\"#CBD5E1\" stroke-width=\"1\"/>");
        for (int i=0;i<d.Count;i++) {
            double bH=(d[i].V/mx)*cH,bx=mL+i*st+(st-bW)/2,by=mT+cH-bH;
            sb.Append($"<rect x=\"{bx:F0}\" y=\"{by:F0}\" width=\"{bW:F0}\" height=\"{bH:F0}\" fill=\"{d[i].C}\" rx=\"3\" opacity=\".9\"/>");
            string vs=d[i].V>=1e6?$"{d[i].V/1e6:F1}M":d[i].V>=1000?$"{d[i].V/1000:F0}K":$"{d[i].V:F0}";
            if(bH>18) sb.Append($"<text x=\"{bx+bW/2:F0}\" y=\"{by+bH/2+4:F0}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#fff\" font-weight=\"700\">{vs}</text>");
            else       sb.Append($"<text x=\"{bx+bW/2:F0}\" y=\"{by-3:F0}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#334155\">{vs}</text>");
            string lb=d[i].L.Length>11?d[i].L[..10]+"...":d[i].L; double lx=bx+bW/2,lya=mT+cH+14;
            sb.Append($"<text x=\"{lx:F0}\" y=\"{lya:F0}\" transform=\"rotate(-35 {lx:F0} {lya:F0})\" text-anchor=\"end\" font-size=\"9\" fill=\"#64748B\">{E(lb)}</text>");
        }
        return sb.Append("</svg>").ToString();
    }

    // â”€â”€ Series extraction â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static List<(string L, double V)> FromPie(IEnumerable<LiveChartsCore.ISeries>? sc)
    {
        if (sc == null) return new();
        var list = new List<(string L, double V)>();
        foreach (var s in sc)
        {
            if (s.Values != null)
            {
                var valEnum = s.Values as System.Collections.IEnumerable;
                if (valEnum != null) {
                    var enumerator = valEnum.GetEnumerator();
                    if (enumerator.MoveNext()) {
                        double val = Convert.ToDouble(enumerator.Current);
                        if (val > 0) list.Add((s.Name ?? "", val));
                    }
                }
            }
        }
        return list;
    }

    private static List<(string L, double V)> FromCol(IEnumerable<LiveChartsCore.ISeries>? sc, IEnumerable<string>? labels)
    {
        var col = sc?.FirstOrDefault();
        if (col?.Values == null) return new();
        var lbls = labels?.ToList() ?? new();
        var valEnum = col.Values as System.Collections.IEnumerable;
        var list = new List<(string L, double V)>();
        if (valEnum != null) {
            int i = 0;
            foreach (var v in valEnum) {
                list.Add((i < lbls.Count ? lbls[i] : $"#{i+1}", Convert.ToDouble(v)));
                i++;
            }
        }
        return list;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  REPORT BUILDERS â€” one per report type
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private static string BuildFinancialOverview(GoodGovernanceApp.ViewModels.Reports.FinancialReportsViewModel vm)
    {
        decimal bal=vm.TotalBudget-vm.TotalExpenses;
        double util=vm.TotalBudget>0?(double)(vm.TotalExpenses/vm.TotalBudget)*100:0;
        string summary=$"As of {DateTime.Now:MMMM dd, yyyy}, the municipality allocated a total budget of {FC(vm.TotalBudget)} "
            +$"and recorded {FC(vm.TotalExpenses)} in expenditures, leaving {FC(bal)} available. "
            +$"Budget utilization is {util:F1}% across {vm.TotalProjects} active projects and {vm.ActiveUsers} active users. "
            +(util>90?"Critically high utilization â€” immediate review required.":util>70?"Elevated utilization â€” monitor closely.":"Utilization is within healthy limits.");
        string kpis=$"<div class=\"kr k4\">{KC("Total Budget",FC(vm.TotalBudget),"#0F172A")}{KC("Total Expenses",FC(vm.TotalExpenses),"#7F1D1D")}{KC("Remaining Balance",FC(bal),bal>=0?"#14532D":"#7C2D12")}{KC("Active Projects",vm.TotalProjects.ToString(),"#1E3A5F")}</div>";
        var dp=FromPie(vm.DeptBudgetSeries);
        string charts=$"<div class=\"cr cr1\"><div class=\"cc\"><h4>Office Budget Distribution</h4>{SvgPie(dp)}</div></div>";
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Key Performance Indicators",kpis));
        body.Append(Sec("Budget Distribution Analysis",Nar("The donut chart illustrates how the total budget is allocated across offices. Disproportionate concentration in one department should be reviewed against actual service delivery outcomes.")+charts));
        body.Append(bal<0?Wbox($"Expenses exceeded budget by {FC(-bal)}. Immediate corrective action required."):util>80?Wbox($"Utilization at {util:F1}%. Only {FC(bal)} remains. Closely monitor spending."):Hbox($"Financial position is healthy. {FC(bal)} remains ({100-util:F1}% of total budget)."));
        return Wrap("Financial Overview","Comprehensive budget and expenditure summary",body.ToString(),"#0F172A","#2563EB");
    }

    private static string BuildConsolidatedAnalytics(GoodGovernanceApp.ViewModels.Reports.TransactionReportsViewModel vm)
    {
        string summary=$"This report consolidates all government transactions. {vm.ConsolidatedTotalCount:N0} transactions were processed "
            +$"totalling {FC(vm.ConsolidatedTotalAmount)}, with an average of {FC(vm.ConsolidatedAvgAmount)} per transaction. "
            +"These figures reflect overall service delivery activity across all offices and programmes.";
        string kpis=$"<div class=\"kr k3\">{KC("Total Transactions",vm.ConsolidatedTotalCount.ToString("N0"),"#1E3A5F")}{KC("Total Amount",FC(vm.ConsolidatedTotalAmount),"#14532D")}{KC("Average Transaction",FC(vm.ConsolidatedAvgAmount),"#312E81")}</div>";
        var tp=FromPie(vm.ConsolidatedTypeSeries); var ml=FromCol(vm.ConsolidatedMonthlySeries,vm.ConsolidatedMonthlyLabels);
        string trend=ml.Count>1?$"Volume peaked in {ml.OrderByDescending(m=>m.V).First().L} and was lowest in {ml.OrderBy(m=>m.V).First().L}.":"Data spans one reporting period.";
        string charts=$"<div class=\"cr cr2\"><div class=\"cc\"><h4>Transactions by Type</h4>{SvgPie(tp)}</div><div class=\"cc\"><h4>Monthly Volume Trend</h4>{SvgBar(ml,"#3B82F6","Count")}</div></div>";
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Transaction KPIs",kpis));
        body.Append(Sec("Transaction Analysis",Nar("The pie chart shows proportion by transaction type. The bar chart tracks monthly volume â€” revealing patterns in service delivery. "+trend+" Anticipating high-activity periods enables better resource planning.")+charts));
        return Wrap("Consolidated Transactions Analytics","System-wide transaction volume and distribution",body.ToString(),"#1E3A5F","#3B82F6");
    }

    private static string BuildCrsAnalytics(GoodGovernanceApp.ViewModels.Reports.BeneficiaryReportsViewModel vm)
    {
        int reg=vm.CrsTotalCount-vm.CrsPwdCount-vm.CrsSeniorCount;
        double pp=vm.CrsTotalCount>0?(double)vm.CrsPwdCount/vm.CrsTotalCount*100:0;
        double sp=vm.CrsTotalCount>0?(double)vm.CrsSeniorCount/vm.CrsTotalCount*100:0;
        using var serviceScope = App.AppHost?.Services.CreateScope();
        var connectivityService = serviceScope?.ServiceProvider.GetService<GoodGovernanceApp.Services.IConnectivityService>();
        bool isCrsOnline = connectivityService?.IsCrsOnline ?? false;
        string src=isCrsOnline?"Live CRS (online)":"Local cache (offline)";
        string summary=$"The CRS holds {vm.CrsTotalCount:N0} registered beneficiaries: {vm.CrsPwdCount:N0} ({pp:F1}%) PWD, "
            +$"{vm.CrsSeniorCount:N0} ({sp:F1}%) Senior Citizens, and {reg:N0} regular community members. Source: {src}.";
        string kpis=$"<div class=\"kr k3\">{KC("Total Beneficiaries",vm.CrsTotalCount.ToString("N0"),"#0F172A")}{KC($"PWD ({pp:F1}%)",vm.CrsPwdCount.ToString("N0"),"#312E81")}{KC($"Senior ({sp:F1}%)",vm.CrsSeniorCount.ToString("N0"),"#7F1D1D")}</div>";
        var gp=FromPie(vm.CrsGenderSeries); var ag=FromCol(vm.CrsAgeGroupSeries,vm.CrsAgeGroupLabels);
        string charts=$"<div class=\"cr cr2\"><div class=\"cc\"><h4>Gender Distribution</h4>{SvgPie(gp)}</div><div class=\"cc\"><h4>Age Group Breakdown</h4>{SvgBar(ag,"#8B5CF6","Beneficiaries")}</div></div>";
        var cls=new[]{("PWD",(double)vm.CrsPwdCount,"#8B5CF6"),("Senior Citizen",(double)vm.CrsSeniorCount,"#EF4444"),("Regular",(double)reg,"#10B981")};
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Beneficiary KPIs",kpis));
        body.Append(Sec("Demographics",Nar("Gender and age charts confirm whether services reach all population segments. Equitable representation across gender and age groups reflects inclusive programme design.")+charts));
        body.Append(Sec("Classification Breakdown",Nar("High PWD/Senior proportions indicate a need for inclusive, accessible service delivery. This informs infrastructure, staffing, and communication strategies.")+PBars(cls)));
        if(!isCrsOnline) body.Append(Wbox("CRS is offline. Data is from the last local cache sync and may not be current."));
        return Wrap("CRS Beneficiary Analytics","Community Relations System demographic report",body.ToString(),"#312E81","#6366F1");
    }

    private static string BuildUserActivityLog(GoodGovernanceApp.ViewModels.Reports.SystemReportsViewModel vm)
    {
        var logs=vm.UserActivityLogs.ToList();
        var byU=logs.GroupBy(l=>l.User?.Name??"System").Select(g=>(N:g.Key,C:g.Count(),Last:g.Max(l=>l.Timestamp))).OrderByDescending(x=>x.C).ToList();
        var byA=logs.GroupBy(l=>l.Action).Select(g=>(A:g.Key,C:g.Count())).OrderByDescending(x=>x.C).Take(8).ToList();
        string summary=$"The audit log contains {logs.Count:N0} events from {byU.Count} users across {byA.Count} action types. "
            +"User activity monitoring enforces accountability and supports compliance by creating a tamper-evident trail of all system operations.";
        string kpis=$"<div class=\"kr k3\">{KC("Total Events",logs.Count.ToString("N0"),"#0F172A")}{KC("Active Users",byU.Count.ToString(),"#1E3A5F")}{KC("Action Types",byA.Count.ToString(),"#14532D")}</div>";
        var bar=byA.Select(x=>(x.A,(double)x.C));
        string chart=$"<div class=\"cc\"><h4>Top Actions by Frequency</h4>{SvgBar(bar,"#0EA5E9","Count")}</div>";
        var urows=byU.Select(u=>new[]{E(u.N),u.C.ToString(),u.Last.ToString("MMM dd, yyyy HH:mm")});
        var lrows=logs.Take(100).Select(l=>new[]{l.Timestamp.ToString("MMM dd HH:mm:ss"),E(l.User?.Name??"â€”"),E(l.Action),E(l.Details??"â€”")});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Activity KPIs",kpis));
        body.Append(Sec("Action Frequency",Nar("High-frequency actions represent core workflows. Unusual spikes in delete/modify operations may indicate unauthorised access and require investigation.")+chart));
        body.Append(Sec("Activity per User",Nar("Total actions per user and most recent timestamp provide a quick overview of who is most active and when.")+Tbl(new[]{"User","Actions","Last Active"},urows)));
        body.Append(Sec($"Detailed Log (latest {Math.Min(100,logs.Count)} entries)",Tbl(new[]{"Timestamp","User","Action","Details"},lrows)));
        return Wrap("User Activity Log","System audit trail and user action history",body.ToString(),"#0F172A","#0EA5E9");
    }

    private static string BuildBudgetSummary(GoodGovernanceApp.ViewModels.Reports.FinancialReportsViewModel vm)
    {
        var rows=vm.BudgetSummaries.OfType<object>().Select(o=>(dynamic)o).ToList();
        string summary=$"This report covers budget allocation and expenditure across {rows.Count} programme categories. "
            +"Understanding spending by category helps programme managers identify overspending and reallocate funds proactively.";
        string kpis=$"<div class=\"kr k2\">{KC("Budget Categories",rows.Count.ToString(),"#0F172A")}{KC("Report Date",DateTime.Now.ToString("MMM dd, yyyy"),"#14532D")}</div>";
        var bd=rows.Select((r,i)=>{try{return(L:(string)r.CategoryName,V:(double)(decimal)r.TotalBudget);}catch{return($"Cat{i+1}",0d);}});
        string chart=$"<div class=\"cc\"><h4>Budget by Category</h4>{SvgBar(bd,"#10B981","Budget")}</div>";
        var tr=rows.Select(r=>{try{decimal b=(decimal)r.TotalBudget,e=(decimal)r.TotalExpenses,bl=(decimal)r.RemainingBalance;
            double u=b>0?(double)(e/b)*100:0; string pill=u>90?"<span class='pl pr'>Critical</span>":u>70?"<span class='pl po'>High</span>":"<span class='pl pg'>Normal</span>";
            return new[]{E((string)r.CategoryName),FC(b),FC(e),FC(bl),$"{u:F1}%",pill};}catch{return new[]{"â€”","â€”","â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Overview",kpis));
        body.Append(Sec("Budget by Category",chart));
        body.Append(Sec("Category Detail",Nar("Categories at Critical (>90%) or High (>70%) utilization need immediate attention to prevent overspending and service disruption.")+Tbl(new[]{"Category","Budget","Expenses","Balance","Utilization","Status"},tr)));
        return Wrap("Budget Summary by Category","Expenditure analysis by programme category",body.ToString(),"#14532D","#10B981");
    }

    private static string BuildTransactionHistory(GoodGovernanceApp.ViewModels.Reports.TransactionReportsViewModel vm)
    {
        var txns=vm.TransactionHistory.ToList();
        var byT=txns.GroupBy(t=>t.TransactionType??"Unknown").Select(g=>(T:g.Key,C:g.Count(),Total:g.Sum(t=>t.Amount))).OrderByDescending(x=>x.Total).ToList();
        decimal grand=txns.Sum(t=>t.Amount);
        string summary=$"The transaction history contains {txns.Count:N0} records totalling {FC(grand)} across {byT.Count} types. "
            +"This log is the primary audit record for all departmental financial activity.";
        string kpis=$"<div class=\"kr k3\">{KC("Total Records",txns.Count.ToString("N0"),"#0F172A")}{KC("Grand Total",FC(grand),"#14532D")}{KC("Transaction Types",byT.Count.ToString(),"#1E3A5F")}</div>";
        var pie=byT.Select(x=>(x.T,(double)x.Total));
        var trows=byT.Select(x=>new[]{E(x.T),x.C.ToString("N0"),FC(x.Total)});
        var lrows=txns.Take(200).Select(t=>new[]{t.TransactionDate.HasValue?t.TransactionDate.Value.ToString("MMM dd, yyyy"):"â€”",E(t.TransactionType??"â€”"),E(t.Description??"â€”"),E(t.VoucherCode??"â€”"),FC(t.Amount)});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("KPIs",kpis));
        body.Append(Sec("Type Breakdown",Nar("Reviewing type distribution confirms expense categories are used correctly and consistently.")+$"<div class=\"cc\"><h4>Amount by Type</h4>{SvgPie(pie)}</div>"+Tbl(new[]{"Type","Count","Total"},trows)));
        body.Append(Sec($"Records (latest {Math.Min(200,txns.Count)})",Tbl(new[]{"Date","Type","Project Code","Voucher","Amount"},lrows)));
        return Wrap("Transaction History","Complete departmental financial transaction records",body.ToString(),"#0F172A","#3B82F6");
    }

    private static string BuildParametersList(GoodGovernanceApp.ViewModels.Reports.SystemReportsViewModel vm)
    {
        var ps=vm.ParametersList.ToList();
        string summary=$"The system contains {ps.Count} configuration parameters controlling key aspects of GGMS behaviour. "
            +"This report documents all current settings for audit and compliance purposes.";
        var rows=ps.Select(p=>new[]{E(p.Name),E(p.Value),E(p.Description??"â€”")});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("All Parameters",Nar("Parameters define configurable values used throughout GGMS. Changes should be reviewed by the system administrator before applying, as they affect workflows and calculations. All changes are logged in the audit trail.")+Tbl(new[]{"Parameter Name","Value","Description"},rows)));
        return Wrap("Parameters List","System configuration parameters and reference values",body.ToString(),"#0F172A","#64748B");
    }

    private static string BuildOfficeBudgetAllocation(GoodGovernanceApp.ViewModels.Reports.FinancialReportsViewModel vm)
    {
        var allocs=vm.DepartmentalBudgets.OfType<object>().Select(o=>(dynamic)o).ToList();
        decimal grand=0; try{grand=allocs.Sum(a=>(decimal)a.Allocated);}catch{}
        string summary=$"Budget has been distributed across {allocs.Count} offices totalling {FC(grand)}. "
            +"Allocations reflect governance priorities and should align with the Annual Investment Plan (AIP). "
            +"Reviewing per-office allocations against service delivery outcomes is essential for accountability.";
        string kpis=$"<div class=\"kr k3\">{KC("Offices Funded",allocs.Count.ToString(),"#0F172A")}{KC("Total Allocated",FC(grand),"#14532D")}{KC("Report Date",DateTime.Now.ToString("MMM dd, yyyy"),"#1E3A5F")}</div>";
        var bar=allocs.Select((a,i)=>{try{return(L:(string)a.DepartmentName,V:(double)(decimal)a.Allocated,C:Palette[i%Palette.Length]);}catch{return($"Office{i+1}",0d,Palette[i%Palette.Length]);}});
        var prog=allocs.Select((a,i)=>{try{return(L:(string)a.DepartmentName,V:(double)(decimal)a.Allocated,C:Palette[i%Palette.Length]);}catch{return($"Office{i+1}",0d,Palette[i%Palette.Length]);}});
        string chart=$"<div class=\"cc\"><h4>Budget per Office</h4>{SvgMBar(bar,"Allocated")}</div>";
        var tr=allocs.Select(a=>{try{decimal al=(decimal)a.Allocated;double pct=grand>0?(double)(al/grand)*100:0;
            return new[]{E((string)a.DepartmentName),E((string)a.Year),FC(al),$"{pct:F1}%"};}catch{return new[]{"â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Allocation KPIs",kpis));
        body.Append(Sec("Distribution",Nar("Offices with disproportionately large allocations should demonstrate equivalent programme impact and service delivery outcomes.")+chart+PBars(prog)));
        body.Append(Sec("Office Allocation Detail",Tbl(new[]{"Office","Year","Allocated","Share of Total"},tr)));
        return Wrap("Office Budget Allocation","Annual budget distribution by government office",body.ToString(),"#14532D","#10B981");
    }

    private static string BuildSystemOverview(GoodGovernanceApp.ViewModels.Reports.SystemReportsViewModel vm)
    {
        var items=vm.SystemOverview.OfType<object>().Select(o=>(dynamic)o).ToList();
        string summary=$"System-wide metrics snapshot as of {DateTime.Now:MMMM dd, yyyy}. "
            +"Designed for executive briefings, board presentations, and compliance documentation.";
        var sb2=new StringBuilder("<div class=\"kr k4\">");
        string[] bgs={"#0F172A","#14532D","#312E81","#7F1D1D"}; int ki=0;
        foreach(var it in items){try{sb2.Append(KC((string)it.Metric,(string)it.Value,bgs[ki%bgs.Length]));ki++;}catch{}}
        sb2.Append("</div>");
        var tr=items.Select(i=>{try{return new[]{E((string)i.Metric),$"<strong>{E((string)i.Value)}</strong>"};}catch{return new[]{"â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Metrics at a Glance",sb2.ToString()));
        body.Append(Sec("All System Metrics",Nar("Figures are live database counts at the time of generation. For detailed breakdowns, refer to individual reports.")+Tbl(new[]{"Metric","Value"},tr)));
        return Wrap("System Overview","Executive dashboard â€” all key system metrics",body.ToString(),"#0F172A","#334155");
    }

    private static string BuildBeneficiariesPerProject(GoodGovernanceApp.ViewModels.Reports.BeneficiaryReportsViewModel vm)
    {
        var rows=vm.BeneficiariesPerProject.OfType<object>().Select(o=>(dynamic)o).ToList();
        int tb=0; decimal ta=0; try{tb=rows.Sum(r=>(int)r.BeneficiaryCount);ta=rows.Sum(r=>(decimal)r.TotalAmount);}catch{}
        string summary=$"Across {rows.Count} projects, {tb:N0} beneficiary interactions have been recorded representing {FC(ta)} in services. "
            +"This report helps measure each project's community reach and service impact.";
        string kpis=$"<div class=\"kr k3\">{KC("Projects",rows.Count.ToString(),"#0F172A")}{KC("Beneficiary Interactions",tb.ToString("N0"),"#14532D")}{KC("Total Services Value",FC(ta),"#1E3A5F")}</div>";
        var bar=rows.Take(12).Select((r,i)=>{try{return(L:(string)r.ProjectName,V:(double)(int)r.BeneficiaryCount);}catch{return($"P{i+1}",0d);}});
        string chart=$"<div class=\"cc\"><h4>Beneficiaries per Project (Top 12)</h4>{SvgBar(bar,"#10B981","Beneficiaries")}</div>";
        var tr=rows.Select(r=>{try{return new[]{E((string)r.ProjectName),E((string)r.ProjectCode),((int)r.BeneficiaryCount).ToString("N0"),((int)r.TransactionCount).ToString("N0"),FC((decimal)r.TotalAmount)};}catch{return new[]{"â€”","â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Project Reach KPIs",kpis));
        body.Append(Sec("Beneficiary Reach",Nar("Projects with higher counts demonstrate broader impact. Comparing reach with allocations reveals cost-effectiveness.")+chart));
        body.Append(Sec("Full Breakdown",Tbl(new[]{"Project Name","Code","Beneficiaries","Transactions","Total Amount"},tr)));
        return Wrap("Beneficiaries per Project","Reach and service delivery analysis by project",body.ToString(),"#14532D","#10B981");
    }

    private static string BuildIndividualBeneficiaries(GoodGovernanceApp.ViewModels.Reports.BeneficiaryReportsViewModel vm)
    {
        var rows=vm.IndividualBeneficiaries.OfType<object>().Select(o=>(dynamic)o).ToList();
        decimal ta=0; try{ta=rows.Sum(r=>(decimal)r.TotalAmount);}catch{}
        string summary=$"{rows.Count:N0} unique beneficiaries received {FC(ta)} in combined services. "
            +"This report supports equity analysis and ensures assistance is distributed fairly across the community.";
        string kpis=$"<div class=\"kr k3\">{KC("Unique Beneficiaries",rows.Count.ToString("N0"),"#0F172A")}{KC("Total Services Value",FC(ta),"#14532D")}{KC("Avg per Beneficiary",rows.Count>0?FC(ta/rows.Count):"â€”","#312E81")}</div>";
        var bar=rows.Take(10).Select((r,i)=>{try{return(L:(string)r.FullName,V:(double)(decimal)r.TotalAmount);}catch{return($"#{i+1}",0d);}});
        string chart=$"<div class=\"cc\"><h4>Top 10 by Amount Received</h4>{SvgBar(bar,"#8B5CF6","Amount")}</div>";
        var tr=rows.Select(r=>{try{return new[]{E((string)r.BeneficiaryId),E((string)r.FullName),((int)r.ServicesReceived).ToString(),((int)r.TotalTransactions).ToString("N0"),FC((decimal)r.TotalAmount),r.LastServiceDate.HasValue?((DateTime)r.LastServiceDate.Value).ToString("MMM dd, yyyy"):"â€”"};}catch{return new[]{"â€”","â€”","â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("KPIs",kpis));
        body.Append(Sec("Top Recipients",Nar("High-value recipients may be enrolled in multiple programmes â€” normal for comprehensive welfare. Disproportionate concentration warrants review to ensure equitable access.")+chart));
        body.Append(Sec("All Beneficiary Records",Tbl(new[]{"Beneficiary ID","Full Name","Service Types","Transactions","Total Amount","Last Service"},tr)));
        return Wrap("Individual Beneficiaries Services Received","Per-person breakdown of government services received",body.ToString(),"#312E81","#6366F1");
    }

    private static string BuildBudgetUtilization(GoodGovernanceApp.ViewModels.Reports.FinancialReportsViewModel vm)
    {
        var rows=vm.BudgetUtilization.OfType<object>().Select(o=>(dynamic)o).ToList();
        decimal tb=0,ts=0; try{tb=rows.Sum(r=>(decimal)r.Budget);ts=rows.Sum(r=>(decimal)r.Spent);}catch{}
        double ov=tb>0?(double)(ts/tb)*100:0;
        string summary=$"Budget utilization covers {rows.Count} active projects. Of {FC(tb)} allocated, {FC(ts)} ({ov:F1}%) spent, leaving {FC(tb-ts)}. "
            +"Projects near 100% risk overspending; those below 30% may face delayed implementation.";
        string kpis=$"<div class=\"kr k4\">{KC("Total Budget",FC(tb),"#0F172A")}{KC("Total Spent",FC(ts),"#7F1D1D")}{KC("Remaining",FC(tb-ts),"#14532D")}{KC("Overall Utilization",$"{ov:F1}%","#312E81")}</div>";
        var bar=rows.Take(12).Select((r,i)=>{try{return(L:(string)r.ProjectName,V:(double)r.UtilizationPct);}catch{return($"P{i+1}",0d);}});
        string chart=$"<div class=\"cc\"><h4>Utilization % per Project (Top 12)</h4>{SvgBar(bar,"#F59E0B","Utilization %")}</div>";
        var tr=rows.Select(r=>{try{double u=(double)r.UtilizationPct;
            string pill=u>=100?"<span class='pl pr'>Over Budget</span>":u>=90?"<span class='pl po'>Critical</span>":u>=60?"<span class='pl pb'>On Track</span>":"<span class='pl pp'>Under-utilised</span>";
            return new[]{E((string)r.ProjectName),E((string)r.ProjectCode),FC((decimal)r.Budget),FC((decimal)r.Spent),FC((decimal)r.Remaining),$"{u:F1}%",pill};}catch{return new[]{"â€”","â€”","â€”","â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Utilization KPIs",kpis));
        body.Append(Sec("Utilization by Project",Nar("Over 90% requires budget revision or scope adjustment. Below 30% signals implementation delays. Healthy range is 50%â€“85% depending on programme timeline.")+chart));
        body.Append(Sec("Project Detail",Tbl(new[]{"Project","Code","Budget","Spent","Remaining","Utilization","Status"},tr)));
        return Wrap("Budget Utilization Report","Expenditure vs budget for all active projects",body.ToString(),"#92400E","#F59E0B");
    }

    private static string BuildProjectStatus(GoodGovernanceApp.ViewModels.Reports.ProjectReportsViewModel vm)
    {
        var rows=vm.ProjectStatusRows.OfType<object>().Select(o=>(dynamic)o).ToList();
        double ap=rows.Count>0?(double)vm.ActiveProjectCount/rows.Count*100:0;
        string summary=$"The portfolio contains {rows.Count} projects: {vm.ActiveProjectCount} active ({ap:F1}%) and {vm.ClosedProjectCount} closed. "
            +"This supports programme oversight with a unified view of implementation status, budget progress, and lifecycle.";
        string kpis=$"<div class=\"kr k3\">{KC("Total Projects",rows.Count.ToString(),"#0F172A")}{KC("Active",vm.ActiveProjectCount.ToString(),"#14532D")}{KC("Closed",vm.ClosedProjectCount.ToString(),"#7F1D1D")}</div>";
        var pie=FromPie(vm.ProjectStatusSeries);
        var bbar=rows.Take(10).Select((r,i)=>{try{return(L:(string)r.ProjectName,V:(double)(decimal)r.Budget);}catch{return($"P{i+1}",0d);}});
        string charts=$"<div class=\"cr cr2\"><div class=\"cc\"><h4>Active vs Closed</h4>{SvgPie(pie)}</div><div class=\"cc\"><h4>Top Projects by Budget</h4>{SvgBar(bbar,"#3B82F6","Budget")}</div></div>";
        var tr=rows.Select(r=>{try{string s=(string)r.Status??"unknown";
            string pill=s=="active"?"<span class='pl pg'>Active</span>":"<span class='pl pr'>Closed</span>";
            return new[]{E((string)r.ProjectName),E((string)r.Office),pill,FC((decimal)r.Budget),FC((decimal)r.Spent),$"{(double)r.UtilizationPct:F1}%"};}catch{return new[]{"â€”","â€”","â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Portfolio KPIs",kpis));
        body.Append(Sec("Status & Budget Analysis",Nar("Healthy portfolios show active projects progressing on schedule with spending aligned to milestones.")+charts));
        body.Append(Sec("Full Project Listing",Tbl(new[]{"Project Name","Office","Status","Budget","Spent","Utilization"},tr)));
        return Wrap("Project Implementation Status Report","Programme portfolio with budget and lifecycle tracking",body.ToString(),"#1E3A5F","#3B82F6");
    }

    private static string BuildPublicServiceDelivery(GoodGovernanceApp.ViewModels.Reports.TransactionReportsViewModel vm)
    {
        var rows=vm.PublicServiceRows.OfType<object>().Select(o=>(dynamic)o).ToList();
        int tb=0; decimal ta=0; try{tb=rows.Sum(r=>(int)r.BeneficiaryCount);ta=rows.Sum(r=>(decimal)r.TotalAmount);}catch{}
        string summary=$"Public service delivery covers {rows.Count} offices. {tb:N0} unique citizens served with {FC(ta)} in services. "
            +"This measures the municipality's direct citizen impact and evaluates service effectiveness across all offices.";
        string kpis=$"<div class=\"kr k3\">{KC("Offices",rows.Count.ToString(),"#0F172A")}{KC("Citizens Served",tb.ToString("N0"),"#14532D")}{KC("Total Services Value",FC(ta),"#1E3A5F")}</div>";
        var bar=rows.Take(12).Select((r,i)=>{try{return(L:(string)r.OfficeName,V:(double)(int)r.BeneficiaryCount);}catch{return($"Office{i+1}",0d);}});
        var prog=rows.Select((r,i)=>{try{return(L:(string)r.OfficeName,V:(double)(int)r.BeneficiaryCount,C:Palette[i%Palette.Length]);}catch{return($"Office{i+1}",0d,Palette[i%Palette.Length]);}});
        string chart=$"<div class=\"cc\"><h4>Citizens Served per Office</h4>{SvgBar(bar,"#06B6D4","Beneficiaries")}</div>";
        var tr=rows.Select(r=>{try{int b=(int)r.BeneficiaryCount;decimal a=(decimal)r.TotalAmount;
            return new[]{E((string)r.OfficeName),b.ToString("N0"),((int)r.TransactionCount).ToString("N0"),FC(a),b>0?FC(a/b):"â€”"};}catch{return new[]{"â€”","â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Service Delivery KPIs",kpis));
        body.Append(Sec("Citizens Served per Office",Nar("Offices reaching more citizens demonstrate higher public impact. Comparing reach with budget allocations reveals the cost-per-beneficiary â€” a key public service efficiency metric.")+chart+PBars(prog)));
        body.Append(Sec("Office Detail",Tbl(new[]{"Office","Beneficiaries","Transactions","Total Amount","Avg per Beneficiary"},tr)));
        return Wrap("Public Service Delivery Report","Citizens reached and services rendered by office",body.ToString(),"#0C4A6E","#06B6D4");
    }

    private static string BuildCitizenFeedback(GoodGovernanceApp.ViewModels.Reports.SystemReportsViewModel vm)
    {
        var rows=vm.CitizenFeedbackRows.OfType<object>().Select(o=>(dynamic)o).ToList();
        double avg=vm.AvgFeedbackScore; int total=vm.TotalFeedbackCount;
        string rating=avg>=90?"Excellent":avg>=75?"Good":avg>=60?"Fair":"Poor";
        string summary=$"Citizen satisfaction data from {total:N0} evaluations yields an average of {avg:F1}/100 â€” '{rating}'. "
            +"Feedback is a core governance indicator demonstrating the municipality's commitment to responsive, citizen-centred service delivery.";
        string kpis=$"<div class=\"kr k3\">{KC("Total Evaluations",total.ToString("N0"),"#0F172A")}{KC("Average Score",$"{avg:F1}/100",avg>=75?"#14532D":avg>=60?"#92400E":"#7F1D1D")}{KC("Overall Rating",rating,"#1E3A5F")}</div>";
        var sd=FromCol(vm.FeedbackScoreSeries,vm.FeedbackScoreLabels);
        string[] bc={"#10B981","#3B82F6","#F59E0B","#EF4444"};
        var multi=sd.Select((d,i)=>(d.L,d.V,bc[i%bc.Length]));
        string chart=$"<div class=\"cc\"><h4>Score Distribution</h4>{SvgMBar(multi,"Evaluations")}</div>";
        var tr=rows.Select(r=>{try{double sc=(double)r.Score;
            string pill=sc>=90?"<span class='pl pg'>Excellent</span>":sc>=75?"<span class='pl pb'>Good</span>":sc>=60?"<span class='pl po'>Fair</span>":"<span class='pl pr'>Poor</span>";
            return new[]{E((string)r.Date),E((string)r.Evaluator),E((string)r.File),$"{sc:F0}",pill,E((string)r.Comments)};}catch{return new[]{"â€”","â€”","â€”","â€”","â€”","â€”"};}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Feedback KPIs",kpis));
        body.Append(Sec("Score Distribution",Nar("Scores band: Excellent (90-100), Good (75-89), Fair (60-74), Poor (<60). A majority in Excellent/Good indicates strong service quality. Poor scores should trigger a service review.")+chart));
        body.Append(avg<75?Wbox($"Average {avg:F1} is below the 75-point target. Develop a service quality improvement plan."):Hbox($"Municipality performing at '{rating}'. Maintain standards and target continuous improvement."));
        body.Append(Sec("Evaluation Records",Tbl(new[]{"Date","Evaluator","File","Score","Rating","Comments"},tr)));
        return Wrap("Citizen Feedback Summary Report","Service quality evaluation and citizen satisfaction analysis",body.ToString(),"#1E3A5F","#6366F1");
    }

    private static string BuildBeneficiaryMasterList(GoodGovernanceApp.ViewModels.Reports.BeneficiaryReportsViewModel vm)
    {
        var rows=vm.BeneficiaryMasterList.OfType<object>().Select(o=>(dynamic)o).ToList();
        bool isCrsOnline = App.AppHost?.Services.GetRequiredService<GoodGovernanceApp.Services.IConnectivityService>().IsCrsOnline ?? false;
        string src=isCrsOnline?"Live CRS (online)":"Local CRS cache (offline)";
        double pp=vm.BmlTotalBeneficiaries>0?(double)vm.BmlPwdCount/vm.BmlTotalBeneficiaries*100:0;
        double sp=vm.BmlTotalBeneficiaries>0?(double)vm.BmlSeniorCount/vm.BmlTotalBeneficiaries*100:0;
        string summary=$"The Beneficiary Master List enriches consolidated transaction records with CRS profiles ({src}). "
            +$"{vm.BmlTotalBeneficiaries:N0} beneficiaries received a combined {FC(vm.BmlTotalAmount)}: "
            +$"{vm.BmlPwdCount:N0} ({pp:F1}%) PWD and {vm.BmlSeniorCount:N0} ({sp:F1}%) Senior Citizens.";
        string kpis=$"<div class=\"kr k4\">{KC("Total Beneficiaries",vm.BmlTotalBeneficiaries.ToString("N0"),"#0F172A")}{KC("Total Received",FC(vm.BmlTotalAmount),"#14532D")}{KC($"PWD ({pp:F1}%)",vm.BmlPwdCount.ToString("N0"),"#312E81")}{KC($"Senior ({sp:F1}%)",vm.BmlSeniorCount.ToString("N0"),"#7F1D1D")}</div>";
        var gp=FromPie(vm.BmlGenderSeries); var cp=FromPie(vm.BmlClassificationSeries);
        var t10=FromCol(vm.BmlTopBeneficiarySeries,vm.BmlTopBeneficiaryLabels); var mo=FromCol(vm.BmlMonthlyTrendSeries,vm.BmlMonthlyTrendLabels);
        string ch1=$"<div class=\"cr cr2\"><div class=\"cc\"><h4>Gender Distribution</h4>{SvgPie(gp)}</div><div class=\"cc\"><h4>Classification Breakdown</h4>{SvgPie(cp)}</div></div>";
        string ch2=$"<div class=\"cr cr2\"><div class=\"cc\"><h4>Top 10 by Amount Received</h4>{SvgBar(t10,"#3B82F6","Amount")}</div><div class=\"cc\"><h4>Monthly Transaction Trend</h4>{SvgBar(mo,"#10B981","Transactions")}</div></div>";
        var tr=rows.Select(r=>{try{
            string pw=(string)r.IsPwd=="Yes"?"<span class='pl pp'>Yes</span>":"No";
            string se=(string)r.IsSenior=="Yes"?"<span class='pl pr'>Yes</span>":"No";
            return new[]{E((string)r.BeneficiaryId),E((string)r.FullName),E((string)r.Sex),E((string)r.Age),E((string)r.Barangay),E((string)r.Address),pw,se,((int)r.ServicesReceived).ToString(),FC((decimal)r.TotalAmount),E((string)r.LastServiceDate)};}
            catch{return Enumerable.Repeat("â€”",11).ToArray();}});
        var body=new StringBuilder();
        body.Append(Exec(summary));
        body.Append(Sec("Master List KPIs",kpis));
        body.Append(Sec("Demographics",Nar("Gender and classification charts confirm whether services reach the most vulnerable groups. High PWD/Senior proportions indicate strong inclusion of priority populations.")+ch1));
        body.Append(Sec("Service Patterns",Nar("Top-10 chart identifies highest-value recipients. Monthly trend reveals whether service delivery is growing or declining â€” consistent growth signals programme expansion and improved community access.")+ch2));
        body.Append(Sec("Complete Beneficiary List",Nar($"Source: {src}. Fields showing 'â€”' indicate unmatched CRS profiles requiring data reconciliation.")+Tbl(new[]{"ID","Full Name","Sex","Age","Barangay","Address","PWD","Senior","Services","Total Received","Last Service"},tr)));
        if(!isCrsOnline) body.Append(Wbox("Profile data from local CRS cache. Reconnect to CRS for accuracy."));
        return Wrap("Beneficiary Master List","Consolidated registry with CRS profile enrichment",body.ToString(),"#0F172A","#6366F1");
    }

    private static string BuildNotAvailable(string t) =>
        Wrap($"{t} â€” Not Available","Export template not yet available",
            $"<div class=\"exec\"><h3>Notice</h3><p>The '{E(t)}' report does not have a printable export template yet.</p></div>");
}

