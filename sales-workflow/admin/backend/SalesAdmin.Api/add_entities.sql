CREATE TABLE delivery_methods (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Type" character varying(50) NOT NULL,
    "Description" text NULL,
    "Price" numeric(18,2) NOT NULL,
    "Active" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_delivery_methods" PRIMARY KEY ("Id")
);

CREATE TABLE store_info (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Address" text NOT NULL,
    "Phone" text NULL,
    "Email" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_store_info" PRIMARY KEY ("Id")
);

CREATE TABLE orders (
    "Id" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "Status" text NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "DeliveryMethod" text NULL,
    "PaymentMethod" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_orders" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_orders_customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES customers ("Id") ON DELETE CASCADE
);

CREATE TABLE order_items (
    "Id" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "Name" text NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric(18,2) NOT NULL,
    "TotalPrice" numeric(18,2) NOT NULL,
    CONSTRAINT "PK_order_items" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_order_items_orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES orders ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_order_items_products_ProductId" FOREIGN KEY ("ProductId") REFERENCES products ("Id") ON DELETE RESTRICT
);

CREATE TABLE conversations (
    "Id" uuid NOT NULL,
    "CustomerId" uuid NULL,
    "SessionId" character varying(100) NOT NULL,
    "Status" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_conversations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_conversations_customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES customers ("Id") ON DELETE SET NULL
);

CREATE TABLE conversation_messages (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "Role" character varying(50) NOT NULL,
    "Content" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_conversation_messages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_conversation_messages_conversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES conversations ("Id") ON DELETE CASCADE
);
