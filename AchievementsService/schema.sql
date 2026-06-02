create table if not exists players (
	steam_id bigint primary key,
	display_name text not null default '',
	created_at timestamptz not null default now(),
	last_seen_at timestamptz not null default now()
);

create table if not exists achievement_progress (
	steam_id bigint not null references players(steam_id) on delete cascade,
	achievement_id text not null,
	current_value integer not null default 0 check (current_value >= 0),
	target_value integer not null check (target_value > 0),
	unlocked_at timestamptz null,
	updated_at timestamptz not null default now(),
	primary key (steam_id, achievement_id)
);

create table if not exists achievement_events (
	event_id text primary key,
	steam_id bigint not null references players(steam_id) on delete cascade,
	source_match_id text not null,
	event_type text not null,
	amount integer not null default 1 check (amount > 0),
	event_value text not null default '',
	payload_hash text not null default '',
	occurred_at timestamptz not null,
	received_at timestamptz not null default now()
);

create index if not exists ix_achievement_events_steam_id_received_at
	on achievement_events (steam_id, received_at desc);

create table if not exists cosmetic_selections (
	steam_id bigint primary key references players(steam_id) on delete cascade,
	selected_piece_id text not null default 'officer_woman',
	selected_dice_skin_id text not null default 'classic',
	updated_at timestamptz not null default now()
);
