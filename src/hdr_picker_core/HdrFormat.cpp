#include "HdrFormat.h"

#include <cmath>
#include <iomanip>
#include <sstream>

namespace hdr
{
namespace
{
std::wstring Fixed(double value, int precision)
{
    if (std::abs(value) < 0.0000005)
    {
        value = 0;
    }

    std::wostringstream stream;
    stream << std::fixed << std::setprecision(precision) << value;
    return stream.str();
}

std::wstring ReplaceAll(std::wstring text, std::wstring const& token, std::wstring const& value)
{
    size_t pos = 0;
    while ((pos = text.find(token, pos)) != std::wstring::npos)
    {
        text.replace(pos, token.length(), value);
        pos += value.length();
    }

    return text;
}

std::wstring TokenValue(HdrColorSample const& sample, std::wstring const& token, int precision)
{
    if (!sample.HasHdrData)
    {
        return L"N/A";
    }

    if (token == L"%Lr")
    {
        return Fixed(sample.Derived.Linear.R, precision);
    }
    if (token == L"%Lg")
    {
        return Fixed(sample.Derived.Linear.G, precision);
    }
    if (token == L"%Lb")
    {
        return Fixed(sample.Derived.Linear.B, precision);
    }
    if (token == L"%Nr")
    {
        return Fixed(sample.Derived.RgbNits.R, precision);
    }
    if (token == L"%Ng")
    {
        return Fixed(sample.Derived.RgbNits.G, precision);
    }
    if (token == L"%Nb")
    {
        return Fixed(sample.Derived.RgbNits.B, precision);
    }
    if (token == L"%Ny")
    {
        return Fixed(sample.Derived.YNits, precision);
    }
    if (token == L"%Ii")
    {
        return Fixed(sample.Derived.Ictcp.I, precision);
    }
    if (token == L"%Ic")
    {
        return std::to_wstring(sample.Derived.IctcpI10);
    }
    if (token == L"%Ct")
    {
        return Fixed(sample.Derived.Ictcp.Ct, precision);
    }
    if (token == L"%Cp")
    {
        return Fixed(sample.Derived.Ictcp.Cp, precision);
    }

    return token;
}

constexpr const wchar_t* Tokens[] = {
    L"%Lr", L"%Lg", L"%Lb",
    L"%Nr", L"%Ng", L"%Nb", L"%Ny",
    L"%Ii", L"%Ic", L"%Ct", L"%Cp",
};
}

std::vector<NamedFormat> DefaultHdrFormats()
{
    return {
        { L"linear RGB", L"linear RGB(%Lr, %Lg, %Lb)", true },
        { L"RGB nits", L"RGB nits(%Nr, %Ng, %Nb)", true },
        { L"Y nits", L"Y nits(%Ny)", true },
        { L"ICtCp", L"ICtCp(I=%Ii, I10=%Ic, Ct=%Ct, Cp=%Cp)", true },
    };
}

std::wstring FormatHdrSample(HdrColorSample const& sample, std::wstring const& formatString, int precision)
{
    std::wstring result = formatString;
    for (auto token : Tokens)
    {
        result = ReplaceAll(result, token, TokenValue(sample, token, precision));
    }

    return result;
}

std::wstring FormatDefaultOutput(HdrColorSample const& sample, int precision)
{
    std::wstring output;
    for (auto const& format : DefaultHdrFormats())
    {
        if (!output.empty())
        {
            output += L"\r\n";
        }

        output += FormatHdrSample(sample, format.Format, precision);
    }

    return output;
}

std::wstring FormatTokenHelp()
{
    return L"HDR tokens are case-sensitive and default to four decimals.\r\n"
           L"%Lr %Lg %Lb: linear RGB\r\n"
           L"%Nr %Ng %Nb: RGB nits, 1.0 = 80 nits\r\n"
           L"%Ny: Y nits\r\n"
           L"%Ii: ICtCp I\r\n"
           L"%Ic: ICtCp I 10-bit PQ code value, round(I * 1023)\r\n"
           L"%Ct %Cp: ICtCp chroma components\r\n"
           L"HDR tokens return N/A when HDR sample data is unavailable.";
}

bool ContainsHdrToken(std::wstring const& formatString)
{
    for (auto token : Tokens)
    {
        if (formatString.find(token) != std::wstring::npos)
        {
            return true;
        }
    }

    return false;
}
}
