<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	�ݒ�
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <h2>�ݒ�</h2>
    <ul>
    <li><a href="<%= Url.Action("Import") %>">�ݒ�̃C���|�[�g</a></li>
    <li><a href="<%= Url.Action("Export") %>">�ݒ�̃G�N�X�|�[�g</a></li>
    </ul>

</asp:Content>
