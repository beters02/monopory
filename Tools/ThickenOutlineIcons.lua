-- Run from the repo root:
--   lua Tools/ThickenOutlineIcons.lua
--   lua Tools/ThickenOutlineIcons.lua 3

local stroke_width = arg and arg[1] or "2.5"

local roots = {
	"Assets/textures/icons/keyboard",
	"Assets/textures/icons/mouse"
}

local function normalize_slashes( path )
	return path:gsub( "\\", "/" )
end

local function quote_path( path )
	return '"' .. path:gsub( '"', '\\"' ) .. '"'
end

local function list_outline_files( root )
	local files = {}
	local command

	if package.config:sub( 1, 1 ) == "\\" then
		command = 'dir /b ' .. quote_path( root .. "\\*_outline.svg" )
	else
		command = 'ls ' .. quote_path( root ) .. '/*_outline.svg'
	end

	local pipe = io.popen( command )
	if not pipe then
		return files
	end

	for line in pipe:lines() do
		if line ~= "" then
			if package.config:sub( 1, 1 ) == "\\" then
				local a = normalize_slashes( root .. "/" .. line )
				table.insert( files, a )
			else
				local a = normalize_slashes( line )
				table.insert( files, a )
			end
		end
	end

	pipe:close()
	return files
end

local function escape_pattern( text )
	return text:gsub( "([^%w])", "%%%1" )
end

local function read_file( path )
	local file = assert( io.open( path, "rb" ) )
	local contents = file:read( "*a" )
	file:close()
	return contents
end

local function write_file( path, contents )
	local file = assert( io.open( path, "wb" ) )
	file:write( contents )
	file:close()
end

local function set_attribute( tag, name, value )
	local attr_pattern = "%s*" .. escape_pattern( name ) .. '="[^"]*"'
	local attr = " " .. name .. '="' .. value .. '"'

	tag = tag:gsub( attr_pattern, "" )
	return tag:gsub( "%s*/?>$", attr .. "%0", 1 )
end

local function thicken_path_tag( tag )
	if not tag:find( 'fill="#FFFFFF"', 1, true ) then
		return tag, false
	end

	if tag:find( 'fill%-opacity="0"' ) then
		return tag, false
	end

	local updated = tag
	updated = set_attribute( updated, "stroke", "#FFFFFF" )
	updated = set_attribute( updated, "stroke-width", stroke_width )
	updated = set_attribute( updated, "stroke-linejoin", "round" )
	updated = set_attribute( updated, "stroke-linecap", "round" )

	return updated, updated ~= tag
end

local changed_files = 0
local changed_paths = 0

for _, root in ipairs( roots ) do
	for _, path in ipairs( list_outline_files( root ) ) do
		local original = read_file( path )
		local path_changes = 0

		local updated = original:gsub( "<path[^>]*>", function( tag )
			local next_tag, changed = thicken_path_tag( tag )
			if changed then
				path_changes = path_changes + 1
			end
			return next_tag
		end )

		if updated ~= original then
			write_file( path, updated )
			changed_files = changed_files + 1
			changed_paths = changed_paths + path_changes
		end
	end
end

print( string.format(
	"Updated %d files and %d visible paths to stroke-width=%s",
	changed_files,
	changed_paths,
	stroke_width
) )
