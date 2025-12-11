<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:wix="http://wixtoolset.org/schemas/v4/wxs">

	<xsl:output method="xml" indent="yes" omit-xml-declaration="yes" />

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()" />
		</xsl:copy>
	</xsl:template>

	<xsl:template match="wix:Component[wix:File[substring(@Source, string-length(@Source) - string-length('Autoprint.Client.exe') + 1) = 'Autoprint.Client.exe']]" />

	<xsl:template match="wix:Component[wix:File[substring(@Source, string-length(@Source) - string-length('Autoprint.Service.exe') + 1) = 'Autoprint.Service.exe']]" />

</xsl:stylesheet>