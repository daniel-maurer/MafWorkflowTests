CREATE TABLE campaigns (
    id uuid PRIMARY KEY,
    name varchar(255) NOT NULL,
    description text NOT NULL,
    start_date timestamptz NOT NULL,
    end_date timestamptz NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    free_shipping boolean NOT NULL DEFAULT false,
    global_discount_percent numeric(5,2) NOT NULL DEFAULT 0,
    discount_1_item numeric(5,2) NOT NULL DEFAULT 0,
    discount_2_items numeric(5,2) NOT NULL DEFAULT 0,
    discount_3_plus_items numeric(5,2) NOT NULL DEFAULT 0,
    custom_rules_json text NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
