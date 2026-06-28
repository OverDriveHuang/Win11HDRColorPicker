#pragma once

#include "HdrSampler.h"

#include <string>
#include <vector>

namespace hdr
{
struct NamedFormat
{
    std::wstring Name;
    std::wstring Format;
    bool VisibleByDefault = false;
};

std::vector<NamedFormat> DefaultHdrFormats();
std::wstring FormatHdrSample(HdrColorSample const& sample, std::wstring const& formatString, int precision = 4);
std::wstring FormatDefaultOutput(HdrColorSample const& sample, int precision = 4);
std::wstring FormatTokenHelp();
bool ContainsHdrToken(std::wstring const& formatString);
}
