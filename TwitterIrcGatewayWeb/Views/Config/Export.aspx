<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
�G�N�X�|�[�g | �ݒ�
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

<h2>�ݒ�̃G�N�X�|�[�g</h2>

<ul>
<li><a href="<%= Url.Action("Export", new { target = "Config" }) %>">��{�ݒ�(Config.xml)</a></li>
<li><a href="<%= Url.Action("Export", new { target = "Groups" }) %>">�O���[�v�ݒ�(Groups.xml)</a></li>
</ul>

</asp:Content>
