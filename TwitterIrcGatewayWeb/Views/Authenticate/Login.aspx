<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Login
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <h2>���O�C��</h2>
    <p><a href="<%= Url.Action("RequestToken", new { BackUrl = ViewData["BackUrl"] }) %>">Twitter�̃T�C�g�Ń��O�C�����܂��B</a></p>

</asp:Content>
