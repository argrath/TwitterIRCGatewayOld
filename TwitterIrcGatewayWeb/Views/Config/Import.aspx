<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
�C���|�[�g | �ݒ�
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

<h2>�ݒ�̃C���|�[�g</h2>
<p>�ȑO���p���Ă���TwitterIrcGateway�̐ݒ�t�@�C�����C���|�[�g�o���܂��B</p>
<p>����: �C���|�[�g����O��TwitterIrcGateway�ɑ΂��邷�ׂĂ̐ڑ���ؒf���Ă��������B�܂��A�A�b�v���[�h����ƌ��݂̐ݒ�͎����܂��B</p>

<div class="blockForm">
<h3>��{�ݒ� (Config.xml)</h3>
<% Html.BeginForm("Import", "Config", new { target = "Config" }, FormMethod.Post, new { enctype = "multipart/form-data" }); %>
�t�@�C��: <input type="file" name="uploadFile" /><br />
<input type="submit" value="�A�b�v���[�h" />
<% Html.EndForm(); %>
<!--/blockForm--></div>

<div class="blockForm">
<h3>�O���[�v�ݒ� (Groups.xml)</h3>
<% Html.BeginForm("Import", "Config", new { target = "Groups" }, FormMethod.Post, new { enctype = "multipart/form-data" }); %>
�t�@�C��: <input type="file" name="uploadFile" /><br />
<input type="submit" value="�A�b�v���[�h" />
<% Html.EndForm(); %>
<!--/blockForm--></div>

</asp:Content>
